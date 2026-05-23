package main

import (
	"encoding/json"
	"fmt"
)

type firewallProfile struct {
	Name    string      `json:"Name"`
	Enabled interface{} `json:"Enabled"`
}

func checkFirewallProfiles() []CheckItem {
	output, err := runPowerShell("Get-NetFirewallProfile | Select-Object Name, Enabled | ConvertTo-Json -Compress")
	if err != nil || output == "" {
		logLine("读取防火墙状态失败：" + output)
		return []CheckItem{{Type: "防火墙", Item: "配置文件状态", Status: statusAbnormal, Description: "未能读取防火墙配置文件状态。"}}
	}

	var profiles []firewallProfile
	if err := json.Unmarshal([]byte(output), &profiles); err != nil {
		var profile firewallProfile
		if err := json.Unmarshal([]byte(output), &profile); err != nil {
			logLine("解析防火墙状态失败：" + err.Error())
			return []CheckItem{{Type: "防火墙", Item: "配置文件状态", Status: statusAbnormal, Description: "解析防火墙配置文件失败。"}}
		}
		profiles = []firewallProfile{profile}
	}

	items := make([]CheckItem, 0, len(profiles))
	for _, profile := range profiles {
		enabled := readFirewallEnabled(profile.Enabled)
		status := statusStopped
		description := "当前为关闭。"
		if enabled {
			status = statusNormal
			description = "当前为开启。"
		}
		items = append(items, CheckItem{Type: "防火墙", Item: profile.Name + " 配置文件", Status: status, Description: description})
	}
	return items
}

func readFirewallEnabled(value interface{}) bool {
	switch typed := value.(type) {
	case bool:
		return typed
	case float64:
		return typed != 0
	case string:
		return typed == "True" || typed == "true" || typed == "1"
	default:
		return false
	}
}

func disableFirewallProfiles() {
	output, err := runCommand("netsh.exe", "advfirewall", "set", "allprofiles", "state", "off")
	if err != nil {
		logLine(fmt.Sprintf("关闭防火墙配置文件失败：%s", output))
		return
	}
	logLine("已执行：netsh advfirewall set allprofiles state off")
}
