using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetCafeGameAssistant.Models;

namespace NetCafeGameAssistant;

public sealed class GoCoreRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<CheckItem>> RunAsync(string command)
    {
        var corePath = GoCoreExtractor.Extract();
        var workingDirectory = AppContext.BaseDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = corePath,
            Arguments = command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.EnvironmentVariables["NETCAFE_HOME"] = workingDirectory;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动 GoRepairCore.exe。");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        }

        return JsonSerializer.Deserialize<List<CheckItem>>(output, JsonOptions)
            ?? [];
    }

    public static string ExportLog()
    {
        Directory.CreateDirectory(GoCoreExtractor.LogDirectory);
        if (!File.Exists(GoCoreExtractor.LatestLogPath))
        {
            File.WriteAllText(GoCoreExtractor.LatestLogPath, string.Empty, Encoding.UTF8);
        }

        var targetPath = Path.Combine(
            GoCoreExtractor.LogDirectory,
            $"GameAssistant_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.Copy(GoCoreExtractor.LatestLogPath, targetPath, overwrite: false);
        return targetPath;
    }
}
