using System.Diagnostics;

namespace CafeGameEnvironmentAssistant.Core;

public static class AppPaths
{
    private const string AppHomeEnvironmentVariable = "HORIZON_APP_HOME";

    public static string ExecutablePath { get; } =
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? AppContext.BaseDirectory;

    public static string ExecutableDirectory { get; } =
        Path.GetDirectoryName(ExecutablePath)
        ?? AppContext.BaseDirectory;

    public static string AppHomeDirectory { get; } =
        Environment.GetEnvironmentVariable(AppHomeEnvironmentVariable)
        ?? ExecutableDirectory;

    public static string OfflinePackagesDirectory { get; } =
        Path.Combine(AppHomeDirectory, "OfflinePackages");

    public static string LogsDirectory { get; } =
        Path.Combine(AppHomeDirectory, "Logs");
}
