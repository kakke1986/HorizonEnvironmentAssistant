using CafeGameEnvironmentAssistant.Core;

namespace CafeGameEnvironmentAssistant;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                AdminHelper.RestartAsAdministrator();
                return;
            }

            Directory.CreateDirectory(AppPaths.OfflinePackagesDirectory);
            EmbeddedPayloadHelper.ExtractAll(AppPaths.ExecutableDirectory);
            LogHelper.InitializeSession();
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => HandleFatalException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                {
                    HandleFatalException(exception);
                }
            };

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            HandleFatalException(ex);
        }
    }

    private static void HandleFatalException(Exception ex)
    {
        try
        {
            LogHelper.Write($"未处理异常：{ex}");
        }
        catch
        {
            // Ignore secondary failures while reporting a fatal startup/runtime error.
        }

        MessageBox.Show(
            "程序遇到未处理异常，已停止当前操作。\r\n\r\n" + ex.Message,
            "地平线环境助手",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
