namespace CafeGameEnvironmentAssistant.Core;

public static class AppxHelper
{
    public static async Task<AppxInstallResult> InstallPackageAsync(string packagePath)
    {
        var packageFileName = Path.GetFileName(packagePath);
        var escapedPackagePath = packagePath.Replace("'", "''");
        var command = $"$ErrorActionPreference = 'Stop'; Add-AppxPackage -Path '{escapedPackagePath}'";
        LogHelper.Write($"开始安装离线包：{packageFileName}");

        var result = await CommandRunner.RunPowerShellAsync(command, AppPaths.AppHomeDirectory);
        if (result.Succeeded)
        {
            LogHelper.Write($"安装完成：{packageFileName}");
            return new AppxInstallResult(true, packageFileName, string.Empty);
        }

        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        LogHelper.Write($"安装失败：{packageFileName}，{message}");
        return new AppxInstallResult(false, packageFileName, message);
    }
}

public sealed record AppxInstallResult(bool Succeeded, string PackageFileName, string Message);
