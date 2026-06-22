package main

import (
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"
)

type Logger struct {
	mu          sync.Mutex
	sessionPath string
}

func NewLogger() *Logger {
	_ = os.MkdirAll(logsDir(), 0755)
	sessionPath := filepath.Join(logsDir(), time.Now().Format("20060102-150405")+".log")
	return &Logger{sessionPath: sessionPath}
}

func (l *Logger) Printf(format string, args ...any) {
	l.Write(fmt.Sprintf(format, args...))
}

func (l *Logger) Write(message string) {
	line := fmt.Sprintf("[%s] %s", time.Now().Format("2006-01-02 15:04:05"), message)

	l.mu.Lock()
	defer l.mu.Unlock()

	_ = os.MkdirAll(logsDir(), 0755)
	appendLine(latestLogPath(), line)
	appendLine(l.sessionPath, line)
}

func appendLine(path string, line string) {
	file, err := os.OpenFile(path, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if err != nil {
		return
	}
	defer file.Close()
	_, _ = file.WriteString(line + "\r\n")
}
