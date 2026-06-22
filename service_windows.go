//go:build windows

package main

import (
	"context"
	"errors"
	"fmt"
	"strings"
	"time"

	"golang.org/x/sys/windows"
	"golang.org/x/sys/windows/svc"
	"golang.org/x/sys/windows/svc/mgr"
)

type ServiceInfo struct {
	Exists    bool
	State     svc.State
	StartType uint32
}

func getServiceInfo(serviceName string) ServiceInfo {
	manager, err := mgr.Connect()
	if err != nil {
		return ServiceInfo{}
	}
	defer manager.Disconnect()

	service, err := manager.OpenService(serviceName)
	if err != nil {
		return ServiceInfo{}
	}
	defer service.Close()

	status, statusErr := service.Query()
	config, configErr := service.Config()
	info := ServiceInfo{Exists: true}
	if statusErr == nil {
		info.State = status.State
	}
	if configErr == nil {
		info.StartType = config.StartType
	}
	return info
}

func checkServices(serviceNames []string, typeName string) []CheckItem {
	items := make([]CheckItem, 0, len(serviceNames))
	for _, serviceName := range serviceNames {
		info := getServiceInfo(serviceName)
		if !info.Exists {
			items = append(items, CheckItem{
				Type:        typeName,
				Item:        serviceName,
				Status:      StatusAbnormal,
				Description: "服务不存在。",
			})
			continue
		}

		status := StatusStopped
		if info.StartType == mgr.StartDisabled {
			status = StatusAbnormal
		} else if info.State == svc.Running {
			status = StatusNormal
		}

		items = append(items, CheckItem{
			Type:        typeName,
			Item:        serviceName,
			Status:      status,
			Description: fmt.Sprintf("当前状态：%s；启动类型：%s。", translateServiceState(info.State), translateStartType(info.StartType)),
		})
	}
	return items
}

func configureAndStartService(ctx context.Context, logger *Logger, serviceName string, startMode string) {
	info := getServiceInfo(serviceName)
	if !info.Exists {
		logger.Printf("服务不存在，已跳过：%s", serviceName)
		return
	}

	setServiceStartType(ctx, logger, serviceName, startMode)
	startServiceIfExists(ctx, logger, serviceName)
}

func setServiceStartType(ctx context.Context, logger *Logger, serviceName string, startMode string) {
	result := runCommand(ctx, logger, "", "sc.exe", "config", serviceName, "start=", startMode)
	if result.Succeeded() {
		logger.Printf("已设置服务启动类型：%s -> %s", serviceName, startModeDisplay(startMode))
		return
	}

	logger.Printf("sc.exe 设置服务启动类型失败：%s：%s，尝试注册表方式。", serviceName, commandMessage(result))

	startValue := uint32(3)
	switch strings.ToLower(startMode) {
	case "auto":
		startValue = 2
	case "disabled":
		startValue = 4
	}

	err := writeRegistryDWord(`SYSTEM\CurrentControlSet\Services\`+serviceName, "Start", startValue)
	if err != nil {
		logger.Printf("注册表方式设置服务启动类型失败：%s：%v", serviceName, err)
		return
	}
	logger.Printf("已通过注册表设置服务启动类型：%s -> %s", serviceName, startModeDisplay(startMode))
}

func startServiceIfExists(ctx context.Context, logger *Logger, serviceName string) {
	manager, err := mgr.Connect()
	if err != nil {
		logger.Printf("连接服务管理器失败：%v", err)
		return
	}
	defer manager.Disconnect()

	service, err := manager.OpenService(serviceName)
	if err != nil {
		logger.Printf("服务不存在，无法启动：%s", serviceName)
		return
	}
	defer service.Close()

	config, _ := service.Config()
	if config.StartType == mgr.StartDisabled {
		logger.Printf("服务仍处于禁用状态，已跳过启动：%s", serviceName)
		return
	}

	status, err := service.Query()
	if err == nil && status.State == svc.Running {
		logger.Printf("服务已在运行：%s", serviceName)
		return
	}

	err = service.Start()
	if err != nil && !errors.Is(err, windows.ERROR_SERVICE_ALREADY_RUNNING) {
		logger.Printf("启动服务失败：%s：%v", serviceName, err)
		return
	}

	if waitForServiceState(ctx, service, svc.Running, 20*time.Second) {
		logger.Printf("服务启动完成：%s", serviceName)
		return
	}
	logger.Printf("等待服务启动超时：%s", serviceName)
}

func waitForServiceState(ctx context.Context, service *mgr.Service, expected svc.State, timeout time.Duration) bool {
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		select {
		case <-ctx.Done():
			return false
		default:
		}

		status, err := service.Query()
		if err == nil && status.State == expected {
			return true
		}
		time.Sleep(250 * time.Millisecond)
	}
	return false
}

func translateServiceState(state svc.State) string {
	switch state {
	case svc.Stopped:
		return "已停止"
	case svc.StartPending:
		return "启动中"
	case svc.StopPending:
		return "停止中"
	case svc.Running:
		return "运行中"
	case svc.ContinuePending:
		return "继续中"
	case svc.PausePending:
		return "暂停中"
	case svc.Paused:
		return "已暂停"
	default:
		return "未知"
	}
}

func translateStartType(startType uint32) string {
	switch startType {
	case mgr.StartAutomatic:
		return "自动"
	case mgr.StartManual:
		return "手动"
	case mgr.StartDisabled:
		return "禁用"
	default:
		return "未知"
	}
}

func startModeDisplay(startMode string) string {
	switch strings.ToLower(startMode) {
	case "auto":
		return "自动"
	case "demand":
		return "手动"
	case "disabled":
		return "禁用"
	default:
		return startMode
	}
}
