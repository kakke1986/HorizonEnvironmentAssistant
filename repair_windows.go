//go:build windows

package main

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

type xboxSystemService struct {
	Name       string
	Display    string
	DLL        string
	Deps       []string
	Privileges []string
}

var xboxSystemServices = []xboxSystemService{
	{
		Name:       "XboxGipSvc",
		Display:    "Xbox Accessory Management Service",
		DLL:        "xboxgipsvc.dll",
		Privileges: []string{"SeTcbPrivilege", "SeImpersonatePrivilege", "SeChangeNotifyPrivilege", "SeCreateGlobalPrivilege"},
	},
	{
		Name:       "XboxNetApiSvc",
		Display:    "Xbox Live Networking Service",
		DLL:        "XboxNetApiSvc.dll",
		Deps:       []string{"BFE", "mpssvc", "IKEEXT", "KeyIso"},
		Privileges: []string{"SeTcbPrivilege", "SeImpersonatePrivilege"},
	},
	{
		Name:    "XblGameSave",
		Display: "Xbox Live Game Save",
		DLL:     "XblGameSave.dll",
		Deps:    []string{"UserManager", "XblAuthManager"},
	},
}

func runNoRestartRepair(ctx context.Context, logger *Logger) error {
	if err := ensureSupportedPackageEnvironment(); err != nil {
		return err
	}

	logger.Write("开始执行免重启修复。")
	setAllowAllTrustedApps(logger)

	for _, serviceName := range []string{"BFE", "MpsSvc"} {
		configureAndStartService(ctx, logger, serviceName, "auto")
	}

	runFirewallCompatibilityRepair(ctx, logger)

	for _, serviceName := range []string{"AppXSVC", "ClipSVC", "InstallService", "BITS", "wuauserv"} {
		configureAndStartService(ctx, logger, serviceName, "demand")
	}

	repairXboxSystemServices(ctx, logger)
	repairGamingServices(ctx, logger)
	startXboxRelatedServices(ctx, logger)

	logger.Write("免重启修复完成。")
	return nil
}

func setAllowAllTrustedApps(logger *Logger) {
	err := writeRegistryDWord(`SOFTWARE\Policies\Microsoft\Windows\Appx`, "AllowAllTrustedApps", 1)
	if err != nil {
		logger.Printf("设置 AllowAllTrustedApps 失败：%v", err)
		return
	}
	logger.Write(`已设置 HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\AllowAllTrustedApps=1。`)
}

func runFirewallCompatibilityRepair(ctx context.Context, logger *Logger) {
	logger.Write("开始执行防火墙兼容修复：关闭防火墙配置文件，但保持 BFE / MpsSvc 服务运行。")
	for _, serviceName := range []string{"BFE", "MpsSvc"} {
		configureAndStartService(ctx, logger, serviceName, "auto")
	}
	disableFirewallProfiles(ctx, logger)
	logger.Write("防火墙兼容修复完成：BFE / MpsSvc 未被停止。")
}

func disableFirewallProfiles(ctx context.Context, logger *Logger) {
	result := runCommand(ctx, logger, "", "netsh.exe", "advfirewall", "set", "allprofiles", "state", "off")
	if result.Succeeded() {
		logger.Write("已执行：netsh advfirewall set allprofiles state off")
		return
	}
	logger.Printf("关闭防火墙配置文件失败：%s", commandMessage(result))
}

func startXboxRelatedServices(ctx context.Context, logger *Logger) {
	logger.Write("开始启动 Xbox 相关服务。")
	for _, serviceName := range xboxServiceNames {
		configureAndStartService(ctx, logger, serviceName, "demand")
	}
	logger.Write("Xbox 相关服务启动流程完成。")
}

func repairGamingServices(ctx context.Context, logger *Logger) {
	logger.Write("开始修复 Gaming Services。")

	registerScript := `
$ErrorActionPreference = 'Continue'
$pkg = Get-AppxPackage -AllUsers Microsoft.GamingServices | Select-Object -First 1
if ($null -ne $pkg) {
    $manifest = Join-Path $pkg.InstallLocation 'AppxManifest.xml'
    if (Test-Path $manifest) {
        Add-AppxPackage -DisableDevelopmentMode -Register $manifest
        Write-Output "已重新注册 Gaming Services。"
    } else {
        Write-Output "Gaming Services 清单文件不存在，跳过重新注册。"
    }
} else {
    Write-Output "未发现已安装的 Gaming Services 包。"
}
`
	result := runPowerShell(ctx, logger, appHomeDir(), registerScript)
	if !result.Succeeded() {
		logger.Printf("重新注册 Gaming Services 失败：%s", commandMessage(result))
	}

	bundlePath := filepath.Join(offlinePackagesDir(), "GamingServices.msixbundle")
	if _, err := os.Stat(bundlePath); err == nil {
		installAppxPackage(ctx, logger, bundlePath)
	} else {
		logger.Write("未找到 OfflinePackages\\GamingServices.msixbundle，跳过离线重装。")
	}

	startServiceIfExists(ctx, logger, "GamingServices")
	startServiceIfExists(ctx, logger, "GamingServicesNet")
	logger.Write("Gaming Services 修复流程完成。")
}

func repairXboxSystemServices(ctx context.Context, logger *Logger) {
	logger.Write("开始修复 Xbox 系统服务定义。")

	for _, serviceName := range []string{"IKEEXT", "KeyIso", "UserManager", "XblAuthManager"} {
		configureAndStartService(ctx, logger, serviceName, "demand")
	}

	for _, definition := range xboxSystemServices {
		systemFile := filepath.Join(os.Getenv("WINDIR"), "System32", definition.DLL)
		if _, err := os.Stat(systemFile); err != nil {
			logger.Printf("Xbox 系统服务文件缺失：%s", definition.DLL)
			runSystemFileRepair(ctx, logger, systemFile)
		}

		ensureXboxServiceDefinition(ctx, logger, definition)
		configureAndStartService(ctx, logger, definition.Name, "demand")
	}

	logger.Write("Xbox 系统服务定义修复流程完成。")
}

func runSystemFileRepair(ctx context.Context, logger *Logger, filePath string) {
	result := runCommand(ctx, logger, "", "dism.exe", "/Online", "/Cleanup-Image", "/RestoreHealth")
	if result.Succeeded() {
		logger.Write("DISM RestoreHealth 完成。")
	} else {
		logger.Printf("DISM RestoreHealth 失败：%s", commandMessage(result))
	}

	result = runCommand(ctx, logger, "", "sfc.exe", "/scanfile="+filePath)
	if result.Succeeded() {
		logger.Printf("SFC scanfile 完成：%s", filePath)
	} else {
		logger.Printf("SFC scanfile 失败：%s", commandMessage(result))
	}
}

func ensureXboxServiceDefinition(ctx context.Context, logger *Logger, definition xboxSystemService) {
	if !getServiceInfo(definition.Name).Exists {
		result := runCommand(
			ctx,
			logger,
			"",
			"sc.exe",
			"create",
			definition.Name,
			"type=",
			"share",
			"start=",
			"demand",
			"error=",
			"normal",
			"binPath=",
			`%SystemRoot%\system32\svchost.exe -k netsvcs -p`,
			"obj=",
			"LocalSystem",
			"DisplayName=",
			definition.Display)
		if result.Succeeded() {
			logger.Printf("已创建服务：%s", definition.Name)
		} else {
			logger.Printf("创建服务失败：%s：%s", definition.Name, commandMessage(result))
		}
	}

	deps := powershellStringArray(definition.Deps)
	privileges := powershellStringArray(definition.Privileges)
	script := fmt.Sprintf(`
$svc = '%s'
$display = '%s'
$dll = '%%SystemRoot%%\System32\%s'
$deps = %s
$privileges = %s
$path = "HKLM:\SYSTEM\CurrentControlSet\Services\$svc"
New-Item -Path $path -Force | Out-Null
New-ItemProperty -Path $path -Name Type -PropertyType DWord -Value 32 -Force | Out-Null
New-ItemProperty -Path $path -Name Start -PropertyType DWord -Value 3 -Force | Out-Null
New-ItemProperty -Path $path -Name ErrorControl -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty -Path $path -Name ImagePath -PropertyType ExpandString -Value '%%SystemRoot%%\system32\svchost.exe -k netsvcs -p' -Force | Out-Null
New-ItemProperty -Path $path -Name ObjectName -PropertyType String -Value 'LocalSystem' -Force | Out-Null
New-ItemProperty -Path $path -Name DisplayName -PropertyType String -Value $display -Force | Out-Null
New-ItemProperty -Path $path -Name ServiceSidType -PropertyType DWord -Value 1 -Force | Out-Null
if ($deps.Count -gt 0) {
    New-ItemProperty -Path $path -Name DependOnService -PropertyType MultiString -Value $deps -Force | Out-Null
} else {
    Remove-ItemProperty -Path $path -Name DependOnService -ErrorAction SilentlyContinue
}
if ($privileges.Count -gt 0) {
    New-ItemProperty -Path $path -Name RequiredPrivileges -PropertyType MultiString -Value $privileges -Force | Out-Null
} else {
    Remove-ItemProperty -Path $path -Name RequiredPrivileges -ErrorAction SilentlyContinue
}
$paramPath = Join-Path $path 'Parameters'
New-Item -Path $paramPath -Force | Out-Null
New-ItemProperty -Path $paramPath -Name ServiceDll -PropertyType ExpandString -Value $dll -Force | Out-Null
New-ItemProperty -Path $paramPath -Name ServiceDllUnloadOnStop -PropertyType DWord -Value 1 -Force | Out-Null
$svchost = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Svchost'
$netsvcs = @((Get-ItemProperty -Path $svchost -Name netsvcs -ErrorAction SilentlyContinue).netsvcs)
if ($netsvcs -notcontains $svc) {
    New-ItemProperty -Path $svchost -Name netsvcs -PropertyType MultiString -Value ($netsvcs + $svc) -Force | Out-Null
}
Write-Output "已修复 Xbox 系统服务定义：$svc"
`, definition.Name, escapePowerShellSingleQuoted(definition.Display), definition.DLL, deps, privileges)

	result := runPowerShell(ctx, logger, appHomeDir(), script)
	if !result.Succeeded() {
		logger.Printf("修复 Xbox 系统服务定义失败：%s：%s", definition.Name, commandMessage(result))
	}
}

func powershellStringArray(values []string) string {
	if len(values) == 0 {
		return "@()"
	}
	quoted := make([]string, 0, len(values))
	for _, value := range values {
		quoted = append(quoted, "'"+escapePowerShellSingleQuoted(value)+"'")
	}
	return "@(" + strings.Join(quoted, ",") + ")"
}

func escapePowerShellSingleQuoted(value string) string {
	return strings.ReplaceAll(value, "'", "''")
}
