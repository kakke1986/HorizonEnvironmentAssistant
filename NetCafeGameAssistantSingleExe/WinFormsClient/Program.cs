using System.Diagnostics;
using System.Security.Principal;

namespace NetCafeGameAssistant;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!IsRunningAsAdministrator())
        {
            RestartAsAdministrator();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(
            e.Exception.Message,
            "网吧游戏环境助手",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

        Application.Run(new MainForm());
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RestartAsAdministrator()
    {
        try
        {
            var executablePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法定位当前程序路径。");

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "需要管理员权限才能继续。\r\n\r\n" + ex.Message,
                "网吧游戏环境助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
