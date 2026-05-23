package main

import (
	"os"
	"path/filepath"
	"strings"
)

var offlinePackages = []string{
	"Microsoft.VCLibs.appx",
	"Microsoft.NET.Native.Framework.appx",
	"Microsoft.NET.Native.Runtime.appx",
	"XboxIdentityProvider.appxbundle",
	"GamingServices.msixbundle",
}

func checkAppxPolicy() CheckItem {
	value := queryRegistryValue(`HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx`, "AllowAllTrustedApps")
	value = strings.TrimSpace(strings.ToLower(value))
	if value == "0x1" || value == "1" {
		return CheckItem{Type: "注册表", Item: "AllowAllTrustedApps", Status: statusNormal, Description: "当前值为 1。"}
	}
	return CheckItem{Type: "注册表", Item: "AllowAllTrustedApps", Status: statusAbnormal, Description: "未设置为 1。"}
}

func setAllowAllTrustedApps() {
	output, err := runCommand("reg.exe", "add", `HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx`, "/v", "AllowAllTrustedApps", "/t", "REG_DWORD", "/d", "1", "/f")
	if err != nil {
		logLine("设置 AllowAllTrustedApps 失败：" + output)
		return
	}
	logLine(`已设置 HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\AllowAllTrustedApps=1。`)
}

func checkOfflinePackages() []CheckItem {
	items := make([]CheckItem, 0, len(offlinePackages))
	offlineDir := filepath.Join(getAppHome(), "OfflinePackages")
	for _, packageName := range offlinePackages {
		fullPath := filepath.Join(offlineDir, packageName)
		if _, err := os.Stat(fullPath); err == nil {
			items = append(items, CheckItem{Type: "离线包", Item: packageName, Status: statusNormal, Description: "文件存在。"})
		} else {
			items = append(items, CheckItem{Type: "离线包", Item: packageName, Status: statusAbnormal, Description: "文件不存在。"})
		}
	}
	return items
}

func installOfflinePackages() {
	offlineDir := filepath.Join(getAppHome(), "OfflinePackages")
	for _, packageName := range offlinePackages {
		fullPath := filepath.Join(offlineDir, packageName)
		if _, err := os.Stat(fullPath); err != nil {
			logLine("离线包不存在，已跳过：" + packageName)
			continue
		}

		escapedPath := strings.ReplaceAll(fullPath, "'", "''")
		script := "$ErrorActionPreference='Stop'; Add-AppxPackage -Path '" + escapedPath + "'"
		logLine("开始安装离线包：" + packageName)
		output, err := runPowerShellInDir(getAppHome(), script)
		if err != nil {
			logLine("安装失败：" + packageName + "，" + output)
			continue
		}
		logLine("安装完成：" + packageName)
	}
}

func killXboxProcesses() {
	for _, processName := range []string{
		"GamingServices.exe",
		"XboxPcApp.exe",
		"XboxAppServices.exe",
		"GameBar.exe",
		"GameBarFTServer.exe",
	} {
		output, err := runCommand("taskkill.exe", "/F", "/IM", processName)
		if err != nil {
			logLine("未发现或无法结束进程：" + processName + "，" + output)
			continue
		}
		logLine("已结束进程：" + processName)
	}
}
