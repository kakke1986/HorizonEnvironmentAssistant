using Microsoft.Win32;

namespace CafeGameEnvironmentAssistant.Core;

public static class XboxSystemServiceRepairHelper
{
    private static readonly XboxSystemServiceDefinition[] Definitions =
    [
        new(
            "XboxGipSvc",
            "Xbox Accessory Management Service",
            "xboxgipsvc.dll",
            [],
            ["SeTcbPrivilege", "SeImpersonatePrivilege", "SeChangeNotifyPrivilege", "SeCreateGlobalPrivilege"]),
        new(
            "XboxNetApiSvc",
            "Xbox Live Networking Service",
            "XboxNetApiSvc.dll",
            ["BFE", "mpssvc", "IKEEXT", "KeyIso"],
            ["SeTcbPrivilege", "SeImpersonatePrivilege"]),
        new(
            "XblGameSave",
            "Xbox Live Game Save",
            "XblGameSave.dll",
            ["UserManager", "XblAuthManager"],
            [])
    ];

    public static IReadOnlyList<XboxSystemServiceFileState> GetFileStates()
    {
        return Definitions
            .Select(definition =>
            {
                var filePath = GetSystemFilePath(definition.FileName);
                return new XboxSystemServiceFileState(
                    definition.ServiceName,
                    definition.FileName,
                    File.Exists(filePath));
            })
            .ToList();
    }

    public static async Task RepairAsync()
    {
        await RepairMissingFilesAsync();
        await StartDependencyServicesAsync();

        foreach (var definition in Definitions)
        {
            await EnsureServiceDefinitionAsync(definition);
            await ServiceHelper.ConfigureAndStartAsync(definition.ServiceName, ServiceStartType.Demand);
        }
    }

    private static async Task RepairMissingFilesAsync()
    {
        var missingFiles = Definitions
            .Where(definition => !File.Exists(GetSystemFilePath(definition.FileName)))
            .ToList();

        if (missingFiles.Count == 0)
        {
            LogHelper.Write("Xbox 系统服务文件检测正常。");
            return;
        }

        LogHelper.Write("发现 Xbox 系统服务文件缺失，开始执行系统组件修复。");
        foreach (var definition in missingFiles)
        {
            LogHelper.Write($"缺失系统文件：{definition.FileName}");
        }

        var dismResult = await CommandRunner.RunAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth");
        LogCommandResult("DISM RestoreHealth", dismResult);

        foreach (var definition in missingFiles)
        {
            var filePath = GetSystemFilePath(definition.FileName);
            var sfcResult = await CommandRunner.RunAsync("sfc.exe", $"/scanfile=\"{filePath}\"");
            LogCommandResult($"SFC scanfile {definition.FileName}", sfcResult);
        }
    }

    private static async Task StartDependencyServicesAsync()
    {
        await ServiceHelper.ConfigureAndStartAsync("BFE", ServiceStartType.Auto);
        await ServiceHelper.ConfigureAndStartAsync("MpsSvc", ServiceStartType.Auto);

        foreach (var dependency in new[] { "IKEEXT", "KeyIso", "UserManager", "XblAuthManager" })
        {
            await ServiceHelper.ConfigureAndStartAsync(dependency, ServiceStartType.Demand);
        }
    }

    private static async Task EnsureServiceDefinitionAsync(XboxSystemServiceDefinition definition)
    {
        var serviceInfo = ServiceHelper.GetServiceInfo(definition.ServiceName);
        if (!serviceInfo.Exists)
        {
            var createResult = await CommandRunner.RunAsync(
                "sc.exe",
                $"create {definition.ServiceName} type= share start= demand error= normal binPath= \"{GetSvchostPath()}\" obj= LocalSystem DisplayName= \"{definition.DisplayName}\"");
            LogCommandResult($"创建服务 {definition.ServiceName}", createResult);
        }

        try
        {
            using var serviceKey = Registry.LocalMachine.CreateSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{definition.ServiceName}",
                writable: true);
            if (serviceKey is null)
            {
                LogHelper.Write($"服务注册表不存在，无法修复：{definition.ServiceName}");
                return;
            }

            serviceKey.SetValue("Type", 0x20, RegistryValueKind.DWord);
            serviceKey.SetValue("Start", 3, RegistryValueKind.DWord);
            serviceKey.SetValue("ErrorControl", 1, RegistryValueKind.DWord);
            serviceKey.SetValue("ImagePath", GetSvchostPath(), RegistryValueKind.ExpandString);
            serviceKey.SetValue("ObjectName", "LocalSystem", RegistryValueKind.String);
            serviceKey.SetValue("ServiceSidType", 1, RegistryValueKind.DWord);
            serviceKey.SetValue("DisplayName", definition.DisplayName, RegistryValueKind.String);

            if (definition.Dependencies.Length > 0)
            {
                serviceKey.SetValue("DependOnService", definition.Dependencies, RegistryValueKind.MultiString);
            }
            else
            {
                serviceKey.DeleteValue("DependOnService", throwOnMissingValue: false);
            }

            if (definition.RequiredPrivileges.Length > 0)
            {
                serviceKey.SetValue(
                    "RequiredPrivileges",
                    definition.RequiredPrivileges,
                    RegistryValueKind.MultiString);
            }
            else
            {
                serviceKey.DeleteValue("RequiredPrivileges", throwOnMissingValue: false);
            }

            using var parametersKey = serviceKey.CreateSubKey("Parameters", writable: true);
            parametersKey?.SetValue(
                "ServiceDll",
                $@"%SystemRoot%\System32\{definition.FileName}",
                RegistryValueKind.ExpandString);
            parametersKey?.SetValue("ServiceDllUnloadOnStop", 1, RegistryValueKind.DWord);

            AddServiceToSvchostGroup(definition.ServiceName);
            LogHelper.Write($"已修复 Xbox 系统服务定义：{definition.ServiceName}");
        }
        catch (Exception ex)
        {
            LogHelper.Write($"修复 Xbox 系统服务定义失败：{definition.ServiceName}，{ex.Message}");
        }
    }

    private static void AddServiceToSvchostGroup(string serviceName)
    {
        using var svchostKey = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Svchost",
            writable: true);
        if (svchostKey is null)
        {
            LogHelper.Write("未找到 Svchost 注册表项，无法修复 netsvcs 组。");
            return;
        }

        var currentServices = (svchostKey.GetValue("netsvcs") as string[]) ?? [];
        if (currentServices.Any(value => string.Equals(value, serviceName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var updatedServices = currentServices.Concat([serviceName]).ToArray();
        svchostKey.SetValue("netsvcs", updatedServices, RegistryValueKind.MultiString);
        LogHelper.Write($"已加入 Svchost netsvcs 组：{serviceName}");
    }

    private static void LogCommandResult(string operation, CommandResult result)
    {
        if (result.Succeeded)
        {
            LogHelper.Write($"{operation} 完成。");
            return;
        }

        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        LogHelper.Write($"{operation} 失败：{message}");
    }

    private static string GetSystemFilePath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            fileName);
    }

    private static string GetSvchostPath()
    {
        return $@"{Environment.GetFolderPath(Environment.SpecialFolder.System)}\svchost.exe -k netsvcs -p";
    }

    private sealed record XboxSystemServiceDefinition(
        string ServiceName,
        string DisplayName,
        string FileName,
        string[] Dependencies,
        string[] RequiredPrivileges);
}

public sealed record XboxSystemServiceFileState(string ServiceName, string FileName, bool Exists);
