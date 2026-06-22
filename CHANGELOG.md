# Changelog

## v45

- 使用 Go 重构主程序，生成单 EXE。
- 移除 C# WinForms / .NET 8 Desktop Runtime 依赖。
- 启动后立即显示界面，首次检测改为后台 goroutine。
- 禁止启动时自动联网检查离线包更新，改为用户点击 `检查更新` 后执行。
- 保留 `OfflinePackages` 目录和 `Logs` 目录日志。
- 保留 Windows 版本、系统位数、Xbox 服务、防火墙服务、`AllowAllTrustedApps`、离线包状态检测。
- 保留 `AllowAllTrustedApps=1`、Xbox 服务启动、Gaming Services 修复、离线安装、防火墙兼容修复。
- 防火墙兼容修复不停止 `BFE` / `MpsSvc`。
- 新增根目录 `build.bat`，输出 `dist\HorizonEnvironmentAssistant.exe`。
- 嵌入 Windows common-controls v6 manifest，修复部分系统启动时报 `TTM_ADDTOOL failed` 的窗口创建失败问题。
- 移除界面右侧实时日志窗口，改为底部 `输出日志` 按钮定位 `Logs\latest.log`。
- 主界面去掉“状态卡片”标题，新增右上角 `关于` 按钮。
- 写入 EXE 文件属性：公司、产品名、文件说明和版权信息。
