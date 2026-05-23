package main

import (
	"encoding/json"
	"fmt"
	"os"
	"runtime"
	"strings"
)

const (
	statusNormal   = "正常"
	statusAbnormal = "异常"
	statusStopped  = "停止"
)

type CheckItem struct {
	Type        string `json:"type"`
	Item        string `json:"item"`
	Status      string `json:"status"`
	Description string `json:"description"`
}

func main() {
	initLogger()

	command := "check"
	if len(os.Args) > 1 {
		command = strings.ToLower(strings.TrimSpace(os.Args[1]))
	}

	logLine("GoRepairCore 启动，命令：" + command)

	switch command {
	case "check":
		writeJSON(collectChecks())
	case "repair":
		runNoRestartRepair()
		writeJSON(collectChecks())
	case "xbox":
		runOfflineXboxRepair()
		writeJSON(collectChecks())
	case "firewall":
		runFirewallCompatibilityRepair()
		writeJSON(collectChecks())
	default:
		logLine("未知命令：" + command)
		writeJSON([]CheckItem{
			{Type: "程序", Item: "命令", Status: statusAbnormal, Description: "未知命令：" + command},
		})
	}
}

func collectChecks() []CheckItem {
	items := make([]CheckItem, 0, 32)
	items = append(items, checkWindowsVersion())
	items = append(items, checkArchitecture())
	items = append(items, checkFirewallProfiles()...)

	items = append(items, checkServices([]string{
		"BFE",
		"MpsSvc",
		"AppXSVC",
		"ClipSVC",
		"InstallService",
		"BITS",
		"wuauserv",
	}, "核心服务")...)

	items = append(items, checkServices([]string{
		"GamingServices",
		"GamingServicesNet",
		"XblAuthManager",
		"XblGameSave",
		"XboxNetApiSvc",
		"XboxGipSvc",
	}, "Xbox 服务")...)

	items = append(items, checkAppxPolicy())
	items = append(items, checkOfflinePackages()...)
	return items
}

func runNoRestartRepair() {
	logLine("开始执行免重启修复。")
	setAllowAllTrustedApps()
	configureAndStartService("BFE", "auto")
	configureAndStartService("MpsSvc", "auto")
	disableFirewallProfiles()
	configureAndStartService("AppXSVC", "demand")
	configureAndStartService("ClipSVC", "demand")
	configureAndStartService("InstallService", "demand")
	configureAndStartService("BITS", "demand")
	configureAndStartService("wuauserv", "demand")
	repairXboxSystemServices()
	tryStartServiceIfExists("GamingServices")
	tryStartServiceIfExists("GamingServicesNet")
	logLine("免重启修复完成。")
}

func runOfflineXboxRepair() {
	logLine("开始执行离线修复 Xbox。")
	runNoRestartRepair()
	installOfflinePackages()
	killXboxProcesses()
	repairXboxSystemServices()
	tryStartServiceIfExists("GamingServices")
	tryStartServiceIfExists("GamingServicesNet")
	logLine("离线修复 Xbox 完成。")
}

func runFirewallCompatibilityRepair() {
	logLine("开始执行防火墙兼容修复。")
	configureAndStartService("BFE", "auto")
	configureAndStartService("MpsSvc", "auto")
	disableFirewallProfiles()
	logLine("防火墙兼容修复完成：配置文件已关闭，BFE 与 MpsSvc 保持运行。")
}

func checkWindowsVersion() CheckItem {
	productName := queryRegistryValue(`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`, "ProductName")
	displayVersion := queryRegistryValue(`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`, "DisplayVersion")
	build := queryRegistryValue(`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`, "CurrentBuildNumber")
	if displayVersion == "" {
		displayVersion = queryRegistryValue(`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`, "ReleaseId")
	}
	if productName == "" {
		productName = "Windows"
	}
	if displayVersion == "" {
		displayVersion = "未知版本"
	}
	description := fmt.Sprintf("%s %s (Build %s)", productName, displayVersion, build)
	return CheckItem{Type: "系统", Item: "Windows 版本", Status: statusNormal, Description: description}
}

func checkArchitecture() CheckItem {
	if runtime.GOARCH == "amd64" || strings.Contains(os.Getenv("PROCESSOR_ARCHITECTURE"), "64") || strings.Contains(os.Getenv("PROCESSOR_ARCHITEW6432"), "64") {
		return CheckItem{Type: "系统", Item: "64 位系统", Status: statusNormal, Description: "当前系统为 64 位。"}
	}
	return CheckItem{Type: "系统", Item: "64 位系统", Status: statusAbnormal, Description: "当前系统不是 64 位。"}
}

func writeJSON(items []CheckItem) {
	encoder := json.NewEncoder(os.Stdout)
	encoder.SetEscapeHTML(false)
	if err := encoder.Encode(items); err != nil {
		logLine("输出 JSON 失败：" + err.Error())
	}
}
