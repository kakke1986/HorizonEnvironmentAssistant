package main

import (
	"os"
	"path/filepath"
)

func appHomeDir() string {
	if override := os.Getenv("HORIZON_ASSISTANT_HOME"); override != "" {
		return override
	}

	exePath, err := os.Executable()
	if err == nil {
		return filepath.Dir(exePath)
	}

	cwd, err := os.Getwd()
	if err == nil {
		return cwd
	}

	return "."
}

func offlinePackagesDir() string {
	return filepath.Join(appHomeDir(), "OfflinePackages")
}

func logsDir() string {
	return filepath.Join(appHomeDir(), "Logs")
}

func latestLogPath() string {
	return filepath.Join(logsDir(), "latest.log")
}

func manifestPath() string {
	return filepath.Join(offlinePackagesDir(), "packages-manifest.json")
}
