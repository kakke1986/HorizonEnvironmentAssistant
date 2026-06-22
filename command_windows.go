//go:build windows

package main

import (
	"bufio"
	"bytes"
	"context"
	"encoding/base64"
	"encoding/binary"
	"errors"
	"io"
	"os/exec"
	"strings"
	"sync"
	"syscall"
	"unicode/utf16"
)

type CommandResult struct {
	ExitCode int
	Stdout   string
	Stderr   string
}

func (r CommandResult) Succeeded() bool {
	return r.ExitCode == 0
}

func runCommand(ctx context.Context, logger *Logger, dir string, name string, args ...string) CommandResult {
	cmd := exec.CommandContext(ctx, name, args...)
	if dir != "" {
		cmd.Dir = dir
	}
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}

	stdout, err := cmd.StdoutPipe()
	if err != nil {
		if logger != nil {
			logger.Printf("创建命令输出管道失败：%s：%v", name, err)
		}
		return CommandResult{ExitCode: -1, Stderr: err.Error()}
	}
	stderr, err := cmd.StderrPipe()
	if err != nil {
		if logger != nil {
			logger.Printf("创建命令错误管道失败：%s：%v", name, err)
		}
		return CommandResult{ExitCode: -1, Stderr: err.Error()}
	}

	if logger != nil {
		logger.Printf("执行命令：%s %s", name, strings.Join(args, " "))
	}

	if err := cmd.Start(); err != nil {
		if logger != nil {
			logger.Printf("启动命令失败：%s：%v", name, err)
		}
		return CommandResult{ExitCode: -1, Stderr: err.Error()}
	}

	var stdoutBuffer bytes.Buffer
	var stderrBuffer bytes.Buffer
	var wg sync.WaitGroup
	wg.Add(2)
	go captureCommandPipe(&wg, stdout, &stdoutBuffer, logger)
	go captureCommandPipe(&wg, stderr, &stderrBuffer, logger)

	waitErr := cmd.Wait()
	wg.Wait()

	exitCode := 0
	if waitErr != nil {
		var exitErr *exec.ExitError
		if errors.As(waitErr, &exitErr) {
			exitCode = exitErr.ExitCode()
		} else if ctx.Err() != nil {
			exitCode = -1
		} else {
			exitCode = -1
		}
	}

	result := CommandResult{
		ExitCode: exitCode,
		Stdout:   strings.TrimSpace(stdoutBuffer.String()),
		Stderr:   strings.TrimSpace(stderrBuffer.String()),
	}
	if logger != nil && !result.Succeeded() {
		logger.Printf("命令结束但返回失败：%s，退出码：%d", name, result.ExitCode)
	}
	return result
}

func captureCommandPipe(wg *sync.WaitGroup, reader io.Reader, buffer *bytes.Buffer, logger *Logger) {
	defer wg.Done()

	scanner := bufio.NewScanner(reader)
	scanner.Buffer(make([]byte, 4096), 1024*1024)
	for scanner.Scan() {
		line := scanner.Text()
		buffer.WriteString(line)
		buffer.WriteString("\n")
		if logger != nil && strings.TrimSpace(line) != "" {
			logger.Write(line)
		}
	}
}

func runPowerShell(ctx context.Context, logger *Logger, dir string, script string) CommandResult {
	bootstrap := "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
		"$OutputEncoding=[Console]::OutputEncoding; " + script
	return runCommand(
		ctx,
		logger,
		dir,
		"powershell.exe",
		"-NoProfile",
		"-ExecutionPolicy",
		"Bypass",
		"-EncodedCommand",
		encodePowerShellCommand(bootstrap))
}

func encodePowerShellCommand(command string) string {
	encoded := utf16.Encode([]rune(command))
	data := make([]byte, len(encoded)*2)
	for i, value := range encoded {
		binary.LittleEndian.PutUint16(data[i*2:], value)
	}
	return base64.StdEncoding.EncodeToString(data)
}

func commandMessage(result CommandResult) string {
	if strings.TrimSpace(result.Stderr) != "" {
		return strings.TrimSpace(result.Stderr)
	}
	return strings.TrimSpace(result.Stdout)
}
