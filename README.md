# HorizonEnvironmentAssistant

`HorizonEnvironmentAssistant` 是一个使用 Go 重构的 Windows 环境检测与修复工具，用于处理 Xbox / Gaming Services / Appx 离线安装相关环境问题。

当前版本主程序为 Go 单 EXE，不依赖 .NET 8 Desktop Runtime。

## 设计目标

- 程序启动后立即显示界面。
- 启动阶段不执行耗时检测，不自动联网。
- 所有检测、修复、离线安装、下载任务都在后台 goroutine 执行，避免卡死界面。
- 日志实时输出到右侧日志窗口，同时写入 `Logs` 目录。
- `OfflinePackages` 目录保留在程序同目录，用于放置离线安装包。

## 保留检测

- Windows 版本
- 系统位数
- `GamingServices`
- `GamingServicesNet`
- `XblAuthManager`
- `XblGameSave`
- `XboxGipSvc`
- `XboxNetApiSvc`
- `AllowAllTrustedApps` 注册表项
- 防火墙服务状态：`BFE`、`MpsSvc`
- `OfflinePackages` 离线包状态

## 保留修复

- 设置 `HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\AllowAllTrustedApps=1`
- 启动 Xbox 相关服务
- 修复 Xbox 系统服务定义
- 重新注册 / 离线安装 Gaming Services
- 离线安装 `.appx` / `.appxbundle` / `.msixbundle`
- 防火墙兼容修复：关闭防火墙配置文件，但不停止 `BFE` / `MpsSvc`

## 界面

主界面采用简洁 WinForms 风格布局：

- 左侧：状态卡片列表
- 底部按钮：`刷新检测`、`免重启修复`、`离线安装`、`检查更新`、`输出日志`、`打开目录`

`检查更新` 是唯一会自动访问网络的按钮。启动时不会自动检查离线包更新。日志不再显示在界面中，点击 `输出日志` 会定位到 `Logs\latest.log`。

## 离线包目录

离线包放在程序同目录的 `OfflinePackages` 文件夹：

```text
OfflinePackages/
|-- Microsoft.VCLibs.x64.appx
|-- Microsoft.NET.Native.Framework.x64.appx
|-- Microsoft.NET.Native.Runtime.x64.appx
|-- XboxIdentityProvider.appxbundle
`-- GamingServices.msixbundle
```

也可以放入其他 `.appx` / `.appxbundle` / `.msixbundle` 文件，点击 `离线安装` 时会按固定依赖包优先、其他文件按名称排序安装。

## 构建

要求：

- Windows
- Go 1.21 或更新版本

执行：

```bat
build.bat
```

输出：

```text
dist\HorizonEnvironmentAssistant.exe
dist\OfflinePackages\
```

## 项目结构

```text
.
|-- main.go                       # UI 和异步任务调度
|-- detect_windows.go             # Windows / 服务 / 注册表 / 离线包检测
|-- repair_windows.go             # 免重启修复、Xbox 服务修复、Gaming Services 修复
|-- offline_packages_windows.go   # 离线 appx / appxbundle / msixbundle 安装
|-- store_client.go               # 用户点击后解析并下载离线包
|-- service_windows.go            # Windows 服务读取、配置和启动
|-- command_windows.go            # 异步命令执行和日志捕获
|-- logger.go                     # Logs 目录日志
|-- manifest.go                   # packages-manifest.json
|-- app.manifest                  # Windows common-controls v6 / DPI manifest
|-- rsrc.syso                     # build.bat 生成的嵌入资源
|-- OfflinePackages/
`-- build.bat
```

## 不会执行的操作

- 启动时不会检查更新。
- 不依赖 .NET 8 Desktop Runtime。
- 不强制重启系统。
- 不停止 `BFE` / `MpsSvc`。
- 不打开 Microsoft Store 链接。
