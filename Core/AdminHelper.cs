using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace CafeGameEnvironmentAssistant.Core;

public static class AdminHelper
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdministrator()
    {
        try
        {
            var executablePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法定位当前可执行文件。");

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"需要管理员权限才能继续。\r\n\r\n{ex.Message}",
                "网吧游戏环境助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
