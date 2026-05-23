package main

import (
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

type serviceInfo struct {
	exists    bool
	state     string
	startType string
}

type xboxSystemService struct {
	name       string
	display    string
	dll        string
	deps       []string
	privileges []string
}

var xboxSystemServices = []xboxSystemService{
	{name: "XboxGipSvc", display: "Xbox Accessory Management Service", dll: "xboxgipsvc.dll", privileges: []string{"SeTcbPrivilege", "SeImpersonatePrivilege", "SeChangeNotifyPrivilege", "SeCreateGlobalPrivilege"}},
	{name: "XboxNetApiSvc", display: "Xbox Live Networking Service", dll: "XboxNetApiSvc.dll", deps: []string{"BFE", "mpssvc", "IKEEXT", "KeyIso"}, privileges: []string{"SeTcbPrivilege", "SeImpersonatePrivilege"}},
	{name: "XblGameSave", display: "Xbox Live Game Save", dll: "XblGameSave.dll", deps: []string{"UserManager", "XblAuthManager"}},
}

func checkServices(serviceNames []string, typeName string) []CheckItem {
	items := make([]CheckItem, 0, len(serviceNames))
	for _, name := range serviceNames {
		info := getServiceInfo(name)
		if !info.exists {
			items = append(items, CheckItem{Type: typeName, Item: name, Status: statusAbnormal, Description: "服务不存在。"})
			continue
		}

		status := statusStopped
		if info.startType == "禁用" {
			status = statusAbnormal
		} else if info.state == "运行中" {
			status = statusNormal
		}
		items = append(items, CheckItem{
			Type:        typeName,
			Item:        name,
			Status:      status,
			Description: fmt.Sprintf("当前状态：%s；启动类型：%s。", info.state, info.startType),
		})
	}
	return items
}

func getServiceInfo(serviceName string) serviceInfo {
	output, err := runCommand("sc.exe", "query", serviceName)
	if err != nil && (strings.Contains(output, "1060") || strings.Contains(strings.ToLower(output), "does not exist")) {
		return serviceInfo{exists: false}
	}
	if err != nil && strings.TrimSpace(output) == "" {
		return serviceInfo{exists: false}
	}

	return serviceInfo{
		exists:    true,
		state:     parseServiceState(output),
		startType: getServiceStartType(serviceName),
	}
}

func parseServiceState(output string) string {
	upper := strings.ToUpper(output)
	switch {
	case strings.Contains(upper, "RUNNING"):
		return "运行中"
	case strings.Contains(upper, "STOPPED"):
		return "已停止"
	case strings.Contains(upper, "START_PENDING"):
		return "启动中"
	case strings.Contains(upper, "STOP_PENDING"):
		return "停止中"
	case strings.Contains(upper, "PAUSED"):
		return "已暂停"
	default:
		return "未知"
	}
}

func getServiceStartType(serviceName string) string {
	value := queryRegistryValue(`HKLM\SYSTEM\CurrentControlSet\Services\`+serviceName, "Start")
	value = strings.TrimSpace(strings.ToLower(value))
	if strings.HasPrefix(value, "0x") {
		parsed, err := strconv.ParseInt(strings.TrimPrefix(value, "0x"), 16, 32)
		if err == nil {
			return translateStartValue(int(parsed))
		}
	}
	parsed, err := strconv.Atoi(value)
	if err == nil {
		return translateStartValue(parsed)
	}
	return "未知"
}

func translateStartValue(value int) string {
	switch value {
	case 2:
		return "自动"
	case 3:
		return "手动"
	case 4:
		return "禁用"
	default:
		return "未知"
	}
}

func configureAndStartService(serviceName string, startMode string) {
	info := getServiceInfo(serviceName)
	if !info.exists {
		logLine("服务不存在，已跳过：" + serviceName)
		return
	}

	setServiceStartType(serviceName, startMode)
	tryStartServiceIfExists(serviceName)
}

func setServiceStartType(serviceName string, startMode string) {
	output, err := runCommand("sc.exe", "config", serviceName, "start=", startMode)
	if err == nil {
		logLine("已设置服务启动类型：" + serviceName + " -> " + startMode)
		return
	}

	logLine("设置服务启动类型失败：" + serviceName + "，" + output + "，尝试注册表方式。")
	startValue := "3"
	if startMode == "auto" {
		startValue = "2"
	}
	if startMode == "disabled" {
		startValue = "4"
	}
	output, err = runCommand("reg.exe", "add", `HKLM\SYSTEM\CurrentControlSet\Services\`+serviceName, "/v", "Start", "/t", "REG_DWORD", "/d", startValue, "/f")
	if err != nil {
		logLine("注册表方式设置服务启动类型失败：" + serviceName + "，" + output)
		return
	}
	logLine("已通过注册表设置服务启动类型：" + serviceName)
}

func tryStartServiceIfExists(serviceName string) {
	info := getServiceInfo(serviceName)
	if !info.exists {
		logLine("服务不存在，无法启动：" + serviceName)
		return
	}
	if info.state == "运行中" {
		logLine("服务已在运行：" + serviceName)
		return
	}
	if info.startType == "禁用" {
		logLine("服务仍处于禁用状态，已跳过启动：" + serviceName)
		return
	}

	output, err := runCommand("sc.exe", "start", serviceName)
	if err != nil && !strings.Contains(output, "1056") {
		logLine("启动服务失败：" + serviceName + "，" + output)
		return
	}
	logLine("已尝试启动服务：" + serviceName)
}

func repairXboxSystemServices() {
	for _, svc := range xboxSystemServices {
		filePath := filepath.Join(os.Getenv("WINDIR"), "System32", svc.dll)
		if _, err := os.Stat(filePath); err != nil {
			logLine("Xbox 系统服务文件缺失：" + svc.dll)
			runSystemFileRepair(filePath)
		}
		ensureXboxServiceDefinition(svc)
		configureAndStartService(svc.name, "demand")
	}
}

func runSystemFileRepair(filePath string) {
	output, err := runCommand("dism.exe", "/Online", "/Cleanup-Image", "/RestoreHealth")
	if err != nil {
		logLine("DISM RestoreHealth 失败：" + output)
	} else {
		logLine("DISM RestoreHealth 完成。")
	}
	output, err = runCommand("sfc.exe", "/scanfile="+filePath)
	if err != nil {
		logLine("SFC scanfile 失败：" + output)
	} else {
		logLine("SFC scanfile 完成：" + filePath)
	}
}

func ensureXboxServiceDefinition(svc xboxSystemService) {
	deps := "@()"
	if len(svc.deps) > 0 {
		deps = "@('" + strings.Join(svc.deps, "','") + "')"
	}
	privileges := "@()"
	if len(svc.privileges) > 0 {
		privileges = "@('" + strings.Join(svc.privileges, "','") + "')"
	}

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
if ($deps.Count -gt 0) { New-ItemProperty -Path $path -Name DependOnService -PropertyType MultiString -Value $deps -Force | Out-Null } else { Remove-ItemProperty -Path $path -Name DependOnService -ErrorAction SilentlyContinue }
if ($privileges.Count -gt 0) { New-ItemProperty -Path $path -Name RequiredPrivileges -PropertyType MultiString -Value $privileges -Force | Out-Null } else { Remove-ItemProperty -Path $path -Name RequiredPrivileges -ErrorAction SilentlyContinue }
$paramPath = Join-Path $path 'Parameters'
New-Item -Path $paramPath -Force | Out-Null
New-ItemProperty -Path $paramPath -Name ServiceDll -PropertyType ExpandString -Value $dll -Force | Out-Null
New-ItemProperty -Path $paramPath -Name ServiceDllUnloadOnStop -PropertyType DWord -Value 1 -Force | Out-Null
$svchost = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Svchost'
$netsvcs = @((Get-ItemProperty -Path $svchost -Name netsvcs).netsvcs)
if ($netsvcs -notcontains $svc) { New-ItemProperty -Path $svchost -Name netsvcs -PropertyType MultiString -Value ($netsvcs + $svc) -Force | Out-Null }
`, svc.name, svc.display, svc.dll, deps, privileges)

	output, err := runPowerShell(script)
	if err != nil {
		logLine("修复 Xbox 系统服务定义失败：" + svc.name + "，" + output)
		return
	}
	logLine("已修复 Xbox 系统服务定义：" + svc.name)
}
