namespace CafeGameEnvironmentAssistant.Core;

public static class AppxHelper
{
    public static async Task InstallPackageAsync(string packagePath)
    {
        var packageFileName = Path.GetFileName(packagePath);
        var escapedPackagePath = packagePath.Replace("'", "''");
        var command = $"Add-AppxPackage -Path '{escapedPackagePath}'";
        LogHelper.Write($"开始安装离线包：{packageFileName}");

        var result = await CommandRunner.RunPowerShellAsync(command, AppPaths.AppHomeDirectory);
        if (result.Succeeded)
        {
            LogHelper.Write($"安装完成：{packageFileName}");
            return;
        }

        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        LogHelper.Write($"安装失败：{packageFileName}，{message}");
    }
}
