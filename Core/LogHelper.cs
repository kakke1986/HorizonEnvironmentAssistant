namespace CafeGameEnvironmentAssistant.Core;

public static class LogHelper
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = AppPaths.LogsDirectory;
    private static readonly string LatestLogPath = Path.Combine(LogDirectory, "latest.log");

    public static event Action<string>? LogWritten;

    public static void InitializeSession()
    {
        Directory.CreateDirectory(LogDirectory);

        lock (SyncRoot)
        {
            File.WriteAllText(LatestLogPath, string.Empty);
        }
    }

    public static void Write(string message)
    {
        Directory.CreateDirectory(LogDirectory);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        lock (SyncRoot)
        {
            File.AppendAllText(LatestLogPath, line + Environment.NewLine);
        }

        LogWritten?.Invoke(line);
    }

    public static string ExportTimestampedLog()
    {
        Directory.CreateDirectory(LogDirectory);
        var exportPath = Path.Combine(LogDirectory, $"GameAssistant_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        lock (SyncRoot)
        {
            if (!File.Exists(LatestLogPath))
            {
                File.WriteAllText(LatestLogPath, string.Empty);
            }

            File.Copy(LatestLogPath, exportPath, overwrite: false);
        }

        return exportPath;
    }
}
