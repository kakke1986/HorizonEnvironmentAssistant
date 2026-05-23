package main

import (
	"encoding/base64"
	"encoding/binary"
	"os/exec"
	"strings"
	"syscall"
	"unicode/utf16"
)

func runCommand(name string, args ...string) (string, error) {
	return runCommandInDir("", name, args...)
}

func runCommandInDir(dir string, name string, args ...string) (string, error) {
	cmd := exec.Command(name, args...)
	if dir != "" {
		cmd.Dir = dir
	}
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	output, err := cmd.CombinedOutput()
	return strings.TrimSpace(string(output)), err
}

func runPowerShell(script string) (string, error) {
	return runPowerShellInDir("", script)
}

func runPowerShellInDir(dir string, script string) (string, error) {
	bootstrap := "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); $OutputEncoding=[Console]::OutputEncoding; " + script
	return runCommandInDir(
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
	bytes := make([]byte, len(encoded)*2)
	for index, value := range encoded {
		binary.LittleEndian.PutUint16(bytes[index*2:], value)
	}
	return base64.StdEncoding.EncodeToString(bytes)
}

func queryRegistryValue(path string, valueName string) string {
	output, err := runCommand("reg.exe", "query", path, "/v", valueName)
	if err != nil {
		return ""
	}

	for _, line := range strings.Split(output, "\n") {
		fields := strings.Fields(strings.TrimSpace(line))
		if len(fields) >= 3 && strings.EqualFold(fields[0], valueName) {
			return strings.Join(fields[2:], " ")
		}
	}
	return ""
}
