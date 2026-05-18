using CafeGameEnvironmentAssistant.Core;

namespace CafeGameEnvironmentAssistant;

internal static class Program
{
    [STAThread]
    private static void Main()
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

        using var startupConfirmForm = new StartupConfirmForm();
        if (startupConfirmForm.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        Application.Run(new MainForm());
    }
}
