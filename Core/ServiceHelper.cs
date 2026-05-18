using System.ServiceProcess;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace CafeGameEnvironmentAssistant.Core;

public static class ServiceHelper
{
    public static ServiceInfo GetServiceInfo(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            var status = MapStatus(service.Status);
            return new ServiceInfo(true, status, GetStartType(serviceName));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return ServiceInfo.NotFound;
        }
    }

    public static async Task ConfigureAndStartAsync(string serviceName, ServiceStartType startType)
    {
        if (!ServiceExists(serviceName))
        {
            LogHelper.Write($"服务不存在，已跳过：{serviceName}");
            return;
        }

        await ConfigureStartTypeAsync(serviceName, startType);
        await TryStartIfExistsAsync(serviceName);
    }

    public static async Task TryStartIfExistsAsync(string serviceName)
    {
        if (!ServiceExists(serviceName))
        {
            LogHelper.Write($"服务不存在，无法启动：{serviceName}");
            return;
        }

        try
        {
            using var service = new ServiceController(serviceName);
            service.Refresh();

            if (service.Status == ServiceControllerStatus.Running)
            {
                LogHelper.Write($"服务已在运行：{serviceName}");
                return;
            }

            if (service.Status == ServiceControllerStatus.StartPending)
            {
                await WaitForStatusAsync(service, ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                LogHelper.Write($"服务启动完成：{serviceName}");
                return;
            }

            service.Start();
            await WaitForStatusAsync(service, ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            LogHelper.Write($"已启动服务：{serviceName}");
        }
        catch (Exception ex)
        {
            LogHelper.Write($"启动服务失败：{serviceName}，{ex.Message}");
        }
    }

    public static string TranslateStatus(ServiceProcessStatus status)
    {
        return status switch
        {
            ServiceProcessStatus.Running => "运行中",
            ServiceProcessStatus.Stopped => "已停止",
            ServiceProcessStatus.StartPending => "启动中",
            ServiceProcessStatus.StopPending => "停止中",
            ServiceProcessStatus.Paused => "已暂停",
            ServiceProcessStatus.PausePending => "暂停中",
            ServiceProcessStatus.ContinuePending => "继续中",
            _ => "未知"
        };
    }

    public static string TranslateStartType(ServiceStartType startType)
    {
        return startType switch
        {
            ServiceStartType.Auto => "自动",
            ServiceStartType.Demand => "手动",
            ServiceStartType.Disabled => "禁用",
            _ => "未知"
        };
    }

    private static bool ServiceExists(string serviceName)
    {
        return GetServiceInfo(serviceName).Exists;
    }

    private static ServiceProcessStatus MapStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Stopped => ServiceProcessStatus.Stopped,
            ServiceControllerStatus.StartPending => ServiceProcessStatus.StartPending,
            ServiceControllerStatus.StopPending => ServiceProcessStatus.StopPending,
            ServiceControllerStatus.Running => ServiceProcessStatus.Running,
            ServiceControllerStatus.ContinuePending => ServiceProcessStatus.ContinuePending,
            ServiceControllerStatus.PausePending => ServiceProcessStatus.PausePending,
            ServiceControllerStatus.Paused => ServiceProcessStatus.Paused,
            _ => ServiceProcessStatus.Unknown
        };
    }

    private static ServiceStartType GetStartType(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        var value = key?.GetValue("Start");

        return value switch
        {
            2 => ServiceStartType.Auto,
            3 => ServiceStartType.Demand,
            4 => ServiceStartType.Disabled,
            _ => ServiceStartType.Unknown
        };
    }

    private static async Task ConfigureStartTypeAsync(string serviceName, ServiceStartType startType)
    {
        var scValue = startType switch
        {
            ServiceStartType.Auto => "auto",
            ServiceStartType.Demand => "demand",
            ServiceStartType.Disabled => "disabled",
            _ => throw new ArgumentOutOfRangeException(nameof(startType), startType, null)
        };

        var result = await CommandRunner.RunAsync("sc.exe", $"config {serviceName} start= {scValue}");
        if (result.Succeeded)
        {
            LogHelper.Write($"已设置服务启动类型：{serviceName} -> {TranslateStartType(startType)}");
            return;
        }

        LogHelper.Write($"设置服务启动类型失败：{serviceName}，{GetCommandMessage(result)}");
    }

    private static string GetCommandMessage(CommandResult result)
    {
        return string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
    }

    private static async Task WaitForStatusAsync(
        ServiceController service,
        ServiceControllerStatus expectedStatus,
        TimeSpan timeout)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < timeout)
        {
            service.Refresh();
            if (service.Status == expectedStatus)
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new System.TimeoutException($"等待服务状态超时：{service.ServiceName}");
    }
}

public sealed record ServiceInfo(bool Exists, ServiceProcessStatus Status, ServiceStartType StartType)
{
    public static ServiceInfo NotFound { get; } = new(false, ServiceProcessStatus.Unknown, ServiceStartType.Unknown);
}

public enum ServiceStartType
{
    Unknown,
    Auto,
    Demand,
    Disabled
}

public enum ServiceProcessStatus
{
    Unknown,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}
