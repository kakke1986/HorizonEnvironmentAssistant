package main

import (
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"
)

const appDataDir = `C:\ProgramData\NetCafeGameAssistant`

var logLock sync.Mutex

func initLogger() {
	_ = os.MkdirAll(getLogDir(), 0755)
}

func logLine(message string) {
	logLock.Lock()
	defer logLock.Unlock()

	_ = os.MkdirAll(getLogDir(), 0755)
	line := fmt.Sprintf("[%s] %s\r\n", time.Now().Format("2006-01-02 15:04:05"), message)
	file, err := os.OpenFile(getLatestLogPath(), os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if err != nil {
		return
	}
	defer file.Close()
	_, _ = file.WriteString(line)
}

func getLogDir() string {
	return filepath.Join(appDataDir, "Logs")
}

func getLatestLogPath() string {
	return filepath.Join(getLogDir(), "latest.log")
}

func getAppHome() string {
	if home := os.Getenv("NETCAFE_HOME"); home != "" {
		return home
	}
	home, err := os.Getwd()
	if err != nil {
		return "."
	}
	return home
}
