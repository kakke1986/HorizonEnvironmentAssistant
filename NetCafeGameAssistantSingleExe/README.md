# 网吧游戏环境助手 · 免重启单 EXE 版

这是一个 C# WinForms + Go 的 Windows 工具：

- C# WinForms 负责界面。
- GoRepairCore 负责底层检测、修复和日志。
- 最终发布一个 `网吧游戏环境助手.exe`。
- GoRepairCore.exe 会作为嵌入资源打进 WinForms 主程序。
- 启动时释放到 `C:\ProgramData\NetCafeGameAssistant\GoRepairCore.exe`。

## 功能

- 自动管理员提权。
- 一键体检。
- 免重启修复。
- 离线修复 Xbox / Gaming Services。
- 防火墙兼容修复。
- 导出日志。
- 不依赖 Microsoft Store。
- 不强制重启。

## GoRepairCore 命令

```text
GoRepairCore.exe check
GoRepairCore.exe repair
GoRepairCore.exe xbox
GoRepairCore.exe firewall
```

GoRepairCore 输出 JSON，WinForms 解析后显示到 DataGridView。

## 离线包目录

离线包放在最终 EXE 同目录的 `OfflinePackages` 文件夹：

```text
OfflinePackages/
  Microsoft.VCLibs.appx
  Microsoft.NET.Native.Framework.appx
  Microsoft.NET.Native.Runtime.appx
  XboxIdentityProvider.appxbundle
  GamingServices.msixbundle
```

## 日志

```text
C:\ProgramData\NetCafeGameAssistant\Logs\latest.log
```

导出日志会生成：

```text
GameAssistant_yyyyMMdd_HHmmss.log
```

## 构建

直接运行：

```bat
build.bat
```

构建流程：

1. 编译 `GoRepairCore.exe`
2. 复制到 `WinFormsClient\Tools\`
3. 发布 WinForms 单文件 EXE

最终输出：

```text
publish\网吧游戏环境助手.exe
```

## 禁止操作

程序不会执行：

- `netsh winsock reset`
- `netsh int ip reset`
- 强制重启
- 禁用 BFE
- 禁用 MpsSvc
- 停止防火墙服务
- 打开 Microsoft Store 链接
