//go:build windows

package main

import (
	"context"
	"os"
	"path/filepath"
	"sort"
	"strings"
)

func installOfflinePackages(ctx context.Context, logger *Logger) error {
	if err := ensureSupportedPackageEnvironment(); err != nil {
		return err
	}

	packages, err := findOfflineInstallPackages()
	if err != nil {
		return err
	}
	if len(packages) == 0 {
		logger.Write("OfflinePackages 中没有发现可安装的 appx / appxbundle / msixbundle 文件。")
		return nil
	}

	logger.Printf("发现 %d 个离线包，开始异步安装。", len(packages))
	for _, packagePath := range packages {
		if ctx.Err() != nil {
			return ctx.Err()
		}
		if ok := installAppxPackage(ctx, logger, packagePath); !ok {
			logger.Printf("离线包安装失败，继续处理后续文件：%s", filepath.Base(packagePath))
		}
	}
	logger.Write("离线包安装流程完成。")
	return nil
}

func installAppxPackage(ctx context.Context, logger *Logger, packagePath string) bool {
	packageName := filepath.Base(packagePath)
	escapedPath := strings.ReplaceAll(packagePath, "'", "''")
	script := "$ErrorActionPreference = 'Stop'; Add-AppxPackage -Path '" + escapedPath + "'"

	logger.Printf("开始安装离线包：%s", packageName)
	result := runPowerShell(ctx, logger, appHomeDir(), script)
	if result.Succeeded() {
		logger.Printf("安装完成：%s", packageName)
		return true
	}

	logger.Printf("安装失败：%s：%s", packageName, commandMessage(result))
	return false
}

func findOfflineInstallPackages() ([]string, error) {
	offlineDir := offlinePackagesDir()
	if err := os.MkdirAll(offlineDir, 0755); err != nil {
		return nil, err
	}

	entries, err := os.ReadDir(offlineDir)
	if err != nil {
		return nil, err
	}

	knownOrder := make(map[string]int, len(offlinePackageNames))
	for index, packageName := range offlinePackageNames {
		knownOrder[strings.ToLower(packageName)] = index
	}

	var packages []string
	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}
		ext := strings.ToLower(filepath.Ext(entry.Name()))
		if ext != ".appx" && ext != ".appxbundle" && ext != ".msixbundle" {
			continue
		}
		packages = append(packages, filepath.Join(offlineDir, entry.Name()))
	}

	sort.Slice(packages, func(i, j int) bool {
		left := strings.ToLower(filepath.Base(packages[i]))
		right := strings.ToLower(filepath.Base(packages[j]))
		leftOrder, leftKnown := knownOrder[left]
		rightOrder, rightKnown := knownOrder[right]
		if leftKnown && rightKnown {
			return leftOrder < rightOrder
		}
		if leftKnown != rightKnown {
			return leftKnown
		}
		return left < right
	})

	return packages, nil
}
