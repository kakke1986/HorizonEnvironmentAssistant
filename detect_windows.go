//go:build windows

package main

import (
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
)

type WindowsVersionInfo struct {
	Family       string
	Display      string
	Build        int
	Architecture string
	Supported    bool
}

func collectChecks(logger *Logger) []CheckItem {
	if logger != nil {
		logger.Write("开始后台检测。")
	}

	items := make([]CheckItem, 0, 24)
	items = append(items, checkWindowsVersion())
	items = append(items, checkArchitecture())
	items = append(items, checkServices(firewallServiceNames, "防火墙服务")...)
	items = append(items, checkServices(xboxServiceNames, "Xbox 服务")...)
	items = append(items, checkTrustedAppsPolicy())
	items = append(items, checkOfflinePackages()...)

	if logger != nil {
		logger.Write("后台检测完成。")
	}
	return items
}

func checkWindowsVersion() CheckItem {
	info := getWindowsVersionInfo()
	status := StatusNormal
	if !info.Supported {
		status = StatusAbnormal
	}
	return CheckItem{
		Type:        "系统",
		Item:        "Windows 版本",
		Status:      status,
		Description: info.Display,
	}
}

func checkArchitecture() CheckItem {
	info := getWindowsVersionInfo()
	status := StatusNormal
	description := "当前系统为 64 位。"
	if info.Architecture != "x64" {
		status = StatusAbnormal
		description = "当前系统不是 64 位。"
	}
	return CheckItem{
		Type:        "系统",
		Item:        "系统位数",
		Status:      status,
		Description: description,
	}
}

func checkTrustedAppsPolicy() CheckItem {
	value, ok := readRegistryDWord(`SOFTWARE\Policies\Microsoft\Windows\Appx`, "AllowAllTrustedApps")
	if ok && value == 1 {
		return CheckItem{
			Type:        "注册表",
			Item:        "AllowAllTrustedApps",
			Status:      StatusNormal,
			Description: "当前值为 1。",
		}
	}
	return CheckItem{
		Type:        "注册表",
		Item:        "AllowAllTrustedApps",
		Status:      StatusAbnormal,
		Description: "未设置为 1。",
	}
}

func checkOfflinePackages() []CheckItem {
	items := make([]CheckItem, 0, len(offlinePackageNames)+1)
	offlineDir := offlinePackagesDir()
	if err := os.MkdirAll(offlineDir, 0755); err != nil {
		return []CheckItem{{
			Type:        "离线包",
			Item:        "OfflinePackages",
			Status:      StatusAbnormal,
			Description: "目录创建失败：" + err.Error(),
		}}
	}

	items = append(items, CheckItem{
		Type:        "离线包",
		Item:        "OfflinePackages",
		Status:      StatusNormal,
		Description: offlineDir,
	})

	for _, packageName := range offlinePackageNames {
		fullPath := filepath.Join(offlineDir, packageName)
		if _, err := os.Stat(fullPath); err == nil {
			items = append(items, CheckItem{
				Type:        "离线包",
				Item:        packageName,
				Status:      StatusNormal,
				Description: "文件存在。",
			})
			continue
		}

		items = append(items, CheckItem{
			Type:        "离线包",
			Item:        packageName,
			Status:      StatusAbnormal,
			Description: "文件不存在。",
		})
	}
	return items
}

func getWindowsVersionInfo() WindowsVersionInfo {
	const currentVersionPath = `SOFTWARE\Microsoft\Windows NT\CurrentVersion`

	productName := readRegistryString(currentVersionPath, "ProductName")
	displayVersion := readRegistryString(currentVersionPath, "DisplayVersion")
	if displayVersion == "" {
		displayVersion = readRegistryString(currentVersionPath, "ReleaseId")
	}

	buildNumberText := readRegistryString(currentVersionPath, "CurrentBuildNumber")
	buildNumber, _ := strconv.Atoi(strings.TrimSpace(buildNumberText))
	ubr, hasUBR := readRegistryDWord(currentVersionPath, "UBR")

	family := "Windows"
	if buildNumber >= 22000 {
		family = "Windows 11"
	} else if buildNumber >= 10240 {
		family = "Windows 10"
	}
	if displayVersion == "" {
		displayVersion = "未知版本"
	}

	buildDisplay := buildNumberText
	if buildDisplay == "" {
		buildDisplay = "未知"
	}
	if hasUBR && buildNumber > 0 {
		buildDisplay = fmt.Sprintf("%d.%d", buildNumber, ubr)
	}

	displayName := family + windowsEditionSuffix(productName)

	return WindowsVersionInfo{
		Family:       family,
		Display:      fmt.Sprintf("%s %s (Build %s)", displayName, displayVersion, buildDisplay),
		Build:        buildNumber,
		Architecture: systemArchitecture(),
		Supported:    buildNumber >= 10240,
	}
}

func windowsEditionSuffix(productName string) string {
	productName = strings.TrimSpace(productName)
	if productName == "" {
		return ""
	}
	if strings.HasPrefix(strings.ToLower(productName), "windows 10") {
		return strings.TrimPrefix(productName, "Windows 10")
	}
	if strings.HasPrefix(strings.ToLower(productName), "windows 11") {
		return strings.TrimPrefix(productName, "Windows 11")
	}
	if strings.EqualFold(productName, "Windows") {
		return ""
	}
	return " " + productName
}

func systemArchitecture() string {
	if runtime.GOARCH == "amd64" || runtime.GOARCH == "arm64" {
		return "x64"
	}
	if strings.Contains(os.Getenv("PROCESSOR_ARCHITEW6432"), "64") {
		return "x64"
	}
	if strings.Contains(os.Getenv("PROCESSOR_ARCHITECTURE"), "64") {
		return "x64"
	}
	return "x86"
}

func ensureSupportedWindowsVersion() error {
	info := getWindowsVersionInfo()
	if info.Supported {
		return nil
	}
	return fmt.Errorf("当前程序仅支持 Windows 10 / Windows 11，当前系统：%s", info.Display)
}

func ensureSupportedPackageEnvironment() error {
	if err := ensureSupportedWindowsVersion(); err != nil {
		return err
	}
	if systemArchitecture() != "x64" {
		return fmt.Errorf("当前功能仅支持 Windows 10 / Windows 11 64 位系统")
	}
	return nil
}
