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
        Application.Run(new MainForm());
    }
}
