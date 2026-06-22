//go:build windows

package main

import (
	"context"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"sync/atomic"
	"syscall"
	"time"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
)

type checkItemModel struct {
	walk.TableModelBase
	items []CheckItem
}

func (m *checkItemModel) RowCount() int {
	return len(m.items)
}

func (m *checkItemModel) Value(row int, col int) interface{} {
	if row < 0 || row >= len(m.items) {
		return ""
	}
	item := m.items[row]
	switch col {
	case 0:
		return item.Type
	case 1:
		return item.Item
	case 2:
		return string(item.Status)
	case 3:
		return item.Progress
	case 4:
		return item.Description
	default:
		return ""
	}
}

type uiApp struct {
	mw              *walk.MainWindow
	table           *walk.TableView
	refreshButton   *walk.PushButton
	repairButton    *walk.PushButton
	installButton   *walk.PushButton
	updateButton    *walk.PushButton
	outputLogButton *walk.PushButton
	openButton      *walk.PushButton
	model           *checkItemModel
	logger          *Logger
	busy            int32
}

func main() {
	_ = os.MkdirAll(offlinePackagesDir(), 0755)
	logger := NewLogger()

	app := &uiApp{
		logger: logger,
		model:  &checkItemModel{items: initialCheckItems()},
	}

	if err := app.createMainWindow(); err != nil {
		walk.MsgBox(nil, "地平线环境助手", "创建窗口失败："+err.Error(), walk.MsgBoxOK|walk.MsgBoxIconError)
		return
	}

	app.mw.Show()
	logger.Write("程序启动，界面已显示。首次检测已放入后台 goroutine。")
	go app.refreshChecks()
	app.mw.Run()
}

func (a *uiApp) createMainWindow() error {
	return MainWindow{
		AssignTo: &a.mw,
		Title:    "地平线环境助手",
		MinSize:  Size{Width: 460, Height: 320},
		Size:     Size{Width: 520, Height: 360},
		Font:     Font{Family: "Microsoft YaHei UI", PointSize: 9},
		Layout:   VBox{Margins: Margins{Left: 8, Top: 8, Right: 8, Bottom: 8}, Spacing: 6},
		Children: []Widget{
			GroupBox{
				Title:         "状态卡片",
				StretchFactor: 1,
				Layout:        VBox{Margins: Margins{Left: 4, Top: 4, Right: 4, Bottom: 4}},
				Children: []Widget{
					TableView{
						AssignTo:            &a.table,
						AlternatingRowBG:    true,
						ColumnsSizable:      true,
						LastColumnStretched: true,
						Model:               a.model,
						Columns: []TableViewColumn{
							{Title: "分类", Width: 72},
							{Title: "项目", Width: 150},
							{Title: "状态", Width: 58},
							{Title: "进度", Width: 58},
							{Title: "说明", Width: 260},
						},
						StyleCell: a.styleStatusCell,
					},
				},
			},
			Composite{
				Layout: HBox{MarginsZero: true, Spacing: 4},
				Children: []Widget{
					PushButton{AssignTo: &a.refreshButton, Text: "刷新检测", OnClicked: a.onRefreshClicked},
					PushButton{AssignTo: &a.repairButton, Text: "免重启修复", OnClicked: a.onRepairClicked},
					PushButton{AssignTo: &a.installButton, Text: "离线安装", OnClicked: a.onInstallClicked},
					PushButton{AssignTo: &a.updateButton, Text: "检查更新", OnClicked: a.onUpdateClicked},
					PushButton{AssignTo: &a.outputLogButton, Text: "输出日志", OnClicked: a.onOutputLogClicked},
					PushButton{AssignTo: &a.openButton, Text: "打开目录", OnClicked: a.onOpenDirClicked},
				},
			},
		},
	}.Create()
}

func (a *uiApp) styleStatusCell(style *walk.CellStyle) {
	if style.Col() != 2 {
		return
	}
	row := style.Row()
	if row < 0 || row >= len(a.model.items) {
		return
	}

	switch a.model.items[row].Status {
	case StatusNormal:
		style.TextColor = walk.RGB(22, 163, 74)
	case StatusStopped, StatusPending, StatusChecking, StatusWorking:
		style.TextColor = walk.RGB(180, 83, 9)
	case StatusAbnormal:
		style.TextColor = walk.RGB(220, 38, 38)
	}
}

func (a *uiApp) setItems(items []CheckItem) {
	a.mw.Synchronize(func() {
		a.model.items = items
		a.model.PublishRowsReset()
	})
}

func (a *uiApp) setButtonsEnabled(enabled bool) {
	a.mw.Synchronize(func() {
		a.refreshButton.SetEnabled(enabled)
		a.repairButton.SetEnabled(enabled)
		a.installButton.SetEnabled(enabled)
		a.updateButton.SetEnabled(enabled)
	})
}

func (a *uiApp) onRefreshClicked() {
	a.runExclusive("刷新检测", false, func(ctx context.Context) error {
		a.setItems([]CheckItem{{Type: "系统", Item: "后台检测", Status: StatusChecking, Description: "正在刷新本机状态。"}})
		a.setItems(collectChecks(a.logger))
		return nil
	})
}

func (a *uiApp) onRepairClicked() {
	a.runExclusive("免重启修复", true, func(ctx context.Context) error {
		if err := runNoRestartRepair(ctx, a.logger); err != nil {
			return err
		}
		a.setItems(collectChecks(a.logger))
		return nil
	})
}

func (a *uiApp) onInstallClicked() {
	a.runExclusive("离线安装", true, func(ctx context.Context) error {
		if err := installOfflinePackages(ctx, a.logger); err != nil {
			return err
		}
		a.setItems(collectChecks(a.logger))
		return nil
	})
}

func (a *uiApp) onUpdateClicked() {
	a.runExclusive("检查离线包更新", false, func(ctx context.Context) error {
		if err := a.checkOfflinePackageUpdates(ctx); err != nil {
			return err
		}
		a.setItems(collectChecks(a.logger))
		return nil
	})
}

func (a *uiApp) onOpenDirClicked() {
	_ = os.MkdirAll(offlinePackagesDir(), 0755)
	cmd := exec.Command("explorer.exe", offlinePackagesDir())
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: false}
	if err := cmd.Start(); err != nil {
		a.showError("打开目录失败", err.Error())
	}
}

func (a *uiApp) onOutputLogClicked() {
	a.logger.Write("用户请求输出日志。")
	_ = os.MkdirAll(logsDir(), 0755)
	logPath := latestLogPath()
	if _, err := os.Stat(logPath); err != nil {
		a.showInfo("输出日志", "日志文件尚未生成。")
		return
	}

	cmd := exec.Command("explorer.exe", "/select,"+logPath)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: false}
	if err := cmd.Start(); err != nil {
		a.showError("输出日志失败", err.Error())
	}
}

func (a *uiApp) runExclusive(name string, requireAdmin bool, action func(context.Context) error) {
	if !atomic.CompareAndSwapInt32(&a.busy, 0, 1) {
		a.showInfo("正在执行", "已有任务正在运行，请等待当前任务完成。")
		return
	}

	if requireAdmin && !isRunningAsAdministrator() {
		atomic.StoreInt32(&a.busy, 0)
		if a.confirm("需要管理员权限", name+" 需要管理员权限执行。是否以管理员身份重新启动程序？") {
			if err := relaunchAsAdministrator(); err != nil {
				a.showError("提权失败", err.Error())
			}
		}
		return
	}

	a.setButtonsEnabled(false)
	a.logger.Printf("开始任务：%s", name)
	go func() {
		defer func() {
			atomic.StoreInt32(&a.busy, 0)
			a.setButtonsEnabled(true)
		}()

		ctx := context.Background()
		if err := action(ctx); err != nil {
			a.logger.Printf("%s 失败：%v", name, err)
			a.showError(name+"失败", err.Error())
			return
		}
		a.logger.Printf("任务完成：%s", name)
	}()
}

func (a *uiApp) refreshChecks() {
	if !atomic.CompareAndSwapInt32(&a.busy, 0, 1) {
		return
	}
	a.setButtonsEnabled(false)
	defer func() {
		atomic.StoreInt32(&a.busy, 0)
		a.setButtonsEnabled(true)
	}()

	a.setItems(collectChecks(a.logger))
}

func (a *uiApp) checkOfflinePackageUpdates(ctx context.Context) error {
	if err := ensureSupportedPackageEnvironment(); err != nil {
		return err
	}
	if err := os.MkdirAll(offlinePackagesDir(), 0755); err != nil {
		return err
	}

	a.logger.Write("用户触发离线包更新检查，开始联网解析。")
	a.setItems(refreshingPackageItems())

	packages, err := resolveRequiredPackages(ctx, a.logger)
	if err != nil {
		return err
	}

	states := buildDownloadStates(packages, loadPackageManifest(a.logger))
	a.setItems(downloadStatesToItems(states))

	needsDownload := false
	for _, state := range states {
		if state.RequiresDownload {
			needsDownload = true
			break
		}
	}
	if !needsDownload {
		a.logger.Write("离线包已是最新。")
		a.showInfo("检查更新", "离线包已是最新。")
		return nil
	}

	if !a.confirm("检查更新", "发现离线包缺失或需要更新，是否开始下载？") {
		a.logger.Write("用户取消了离线包下载。")
		return nil
	}

	for index := range states {
		if !states[index].RequiresDownload {
			continue
		}

		states[index].Status = StatusWorking
		states[index].Description = "正在下载。"
		states[index].Progress = "0%"
		a.setItems(downloadStatesToItems(states))

		targetPath := filepath.Join(offlinePackagesDir(), states[index].Package.TargetFileName)
		lastProgressUpdate := time.Now().Add(-time.Second)
		err := downloadPackage(ctx, a.logger, states[index].Package, targetPath, func(progress downloadProgress) {
			if time.Since(lastProgressUpdate) < 120*time.Millisecond {
				return
			}
			lastProgressUpdate = time.Now()
			states[index].Status = StatusWorking
			states[index].Progress = formatProgress(progress)
			states[index].Description = "正在下载 " + states[index].Progress
			a.setItems(downloadStatesToItems(states))
		})
		if err != nil {
			states[index].Status = StatusAbnormal
			states[index].Description = "下载失败：" + err.Error()
			a.setItems(downloadStatesToItems(states))
			return err
		}

		states[index].Status = StatusNormal
		states[index].Progress = "100%"
		states[index].Description = "下载完成。"
		states[index].RequiresDownload = false
		a.setItems(downloadStatesToItems(states))
		a.logger.Printf("下载完成：%s", states[index].Package.TargetFileName)
	}

	if err := savePackageManifest(packages); err != nil {
		return err
	}
	a.logger.Write("已更新 OfflinePackages\\packages-manifest.json。")
	a.showInfo("检查更新", "离线包下载完成。")
	return nil
}

func (a *uiApp) confirm(title string, message string) bool {
	result := make(chan bool, 1)
	a.mw.Synchronize(func() {
		answer := walk.MsgBox(a.mw, title, message, walk.MsgBoxYesNo|walk.MsgBoxIconQuestion)
		result <- answer == walk.DlgCmdYes
	})
	return <-result
}

func (a *uiApp) showError(title string, message string) {
	a.mw.Synchronize(func() {
		walk.MsgBox(a.mw, title, message, walk.MsgBoxOK|walk.MsgBoxIconError)
	})
}

func (a *uiApp) showInfo(title string, message string) {
	a.mw.Synchronize(func() {
		walk.MsgBox(a.mw, title, message, walk.MsgBoxOK|walk.MsgBoxIconInformation)
	})
}

func initialCheckItems() []CheckItem {
	return []CheckItem{
		{Type: "系统", Item: "Windows 版本", Status: StatusPending, Description: "等待后台检测。"},
		{Type: "系统", Item: "系统位数", Status: StatusPending, Description: "等待后台检测。"},
		{Type: "防火墙服务", Item: "BFE / MpsSvc", Status: StatusPending, Description: "等待后台检测。"},
		{Type: "Xbox 服务", Item: "GamingServices / Xbox", Status: StatusPending, Description: "等待后台检测。"},
		{Type: "注册表", Item: "AllowAllTrustedApps", Status: StatusPending, Description: "等待后台检测。"},
		{Type: "离线包", Item: "OfflinePackages", Status: StatusPending, Description: offlinePackagesDir()},
	}
}

func refreshingPackageItems() []CheckItem {
	items := make([]CheckItem, 0, len(offlinePackageNames))
	for _, packageName := range offlinePackageNames {
		items = append(items, CheckItem{
			Type:        "离线包",
			Item:        packageName,
			Status:      StatusChecking,
			Description: "正在刷新线上信息。",
		})
	}
	return items
}

func downloadStatesToItems(states []DownloadState) []CheckItem {
	items := make([]CheckItem, 0, len(states))
	for _, state := range states {
		items = append(items, CheckItem{
			Type:        "离线包",
			Item:        state.FileName,
			Status:      state.Status,
			Progress:    state.Progress,
			Description: state.Description,
		})
	}
	return items
}

func formatProgress(progress downloadProgress) string {
	if progress.Total > 0 {
		percent := float64(progress.Received) * 100 / float64(progress.Total)
		if percent > 100 {
			percent = 100
		}
		return fmt.Sprintf("%.0f%%", percent)
	}
	return fmt.Sprintf("%s", formatBytes(progress.Received))
}

func formatBytes(value int64) string {
	const unit = 1024
	if value < unit {
		return fmt.Sprintf("%d B", value)
	}
	div, exp := int64(unit), 0
	for n := value / unit; n >= unit; n /= unit {
		div *= unit
		exp++
	}
	return fmt.Sprintf("%.1f %cB", float64(value)/float64(div), "KMGTPE"[exp])
}
