# 地平线环境助手

`地平线环境助手` 是一个基于 C# .NET 8 WinForms 的 Windows 工具，用于检查和修复网吧、游戏环境机中的 Xbox / Gaming Services 相关依赖，不依赖 Microsoft Store，也不会强制重启系统。

## 功能

- 自动申请管理员权限
- 检测 Windows 版本、系统位数、防火墙配置文件状态
- 检测核心服务和 Xbox 服务状态
- 检测 `AllowAllTrustedApps` 注册表项
- 扫描 `OfflinePackages` 离线包目录
- 执行免重启修复
- 执行防火墙兼容修复
- 离线安装 Xbox / Gaming Services 依赖包
- 一键下载所需离线包
- 生成 `packages-manifest.json`
- 启动时检查线上离线包是否有更新
- 启动前显示确认窗口

## 支持的离线包

- `Microsoft.VCLibs.x64.appx`
- `Microsoft.NET.Native.Framework.x64.appx`
- `Microsoft.NET.Native.Runtime.x64.appx`
- `XboxIdentityProvider.appxbundle`
- `GamingServices.msixbundle`

离线包默认放在程序同目录下的 `OfflinePackages` 文件夹中。

## 运行要求

- Windows 10 / Windows 11
- x64 系统
- 已安装 .NET 8 Desktop Runtime
- 需要管理员权限运行

当前仓库提供的是源码。若使用项目内的轻量启动器打包方式，最终 EXE 不内置 .NET 8 运行时，因此目标机器仍然需要安装 .NET 8 Desktop Runtime。

## 主要操作

### 刷新

重新检测当前系统、服务、防火墙和离线包状态。

### 免重启修复

会执行以下操作：

- 设置 `AllowAllTrustedApps=1`
- 尝试启用并启动必要服务
- 关闭防火墙配置文件，但保留防火墙服务运行
- 修复后自动重新检测

### 离线修复

先扫描 `OfflinePackages`：

- 文件存在时显示 `文件存在`
- 文件不存在时显示 `文件不存在`
- 只要存在可安装文件，就会提示是否开始安装

安装顺序：

1. `Microsoft.VCLibs.x64.appx`
2. `Microsoft.NET.Native.Framework.x64.appx`
3. `Microsoft.NET.Native.Runtime.x64.appx`
4. `XboxIdentityProvider.appxbundle`
5. `GamingServices.msixbundle`

### 下载离线包

进入下载列表后会先刷新线上信息：

- 本地缺失或线上版本已变化时，提示是否下载更新
- 下载过程中在列表中显示进度条
- 下载完成后生成 `OfflinePackages/packages-manifest.json`

## 项目结构

```text
.
|-- Assets/
|-- Core/
|-- Launcher/
|-- OfflinePackages/
|-- Payload/
|-- MainForm.cs
|-- Program.cs
|-- StartupConfirmForm.cs
`-- CafeGameEnvironmentAssistant.csproj
```

### 关键模块

- `Core/AdminHelper.cs`：管理员权限检测与提权
- `Core/ServiceHelper.cs`：服务检测、启动和启动类型调整
- `Core/FirewallHelper.cs`：防火墙状态读取和兼容修复
- `Core/AppxHelper.cs`：离线 Appx 安装
- `Core/StorePackageClient.cs`：商店包解析、下载和重试
- `Core/PackageManifestStore.cs`：离线包清单保存与更新检测
- `Core/LogHelper.cs`：日志写入

## 构建

### 编译主程序

```powershell
dotnet build .\CafeGameEnvironmentAssistant.csproj -c Release
```

### 发布主程序负载

```powershell
dotnet publish .\CafeGameEnvironmentAssistant.csproj -c Release -o .\Launcher\Payload /p:SelfContained=false /p:UseAppHost=true
```

### 生成轻量单 EXE 启动器

```powershell
dotnet publish .\Launcher\HorizonLauncher.csproj -c Release -o .\dist\HorizonEnvironmentAssistant-packed
```

生成后的可执行文件位于：

```text
dist\HorizonEnvironmentAssistant-packed\HorizonEnvironmentAssistant.exe
```

## 不会执行的操作

程序明确不会执行以下操作：

- `netsh winsock reset`
- `netsh int ip reset`
- 强制重启电脑
- 禁用 `BFE`
- 禁用 `MpsSvc`
- 停止防火墙服务
- 打开 Microsoft Store 链接

## 说明

- 日志保存在程序目录下的 `Logs` 文件夹。
- `OfflinePackages` 目录默认只保留占位文件，实际安装包不提交到源码仓库。
- `Payload` 和 `Launcher/Payload` 中的发布文件属于生成物，也不提交到源码仓库。
