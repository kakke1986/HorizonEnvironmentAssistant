using System.Diagnostics;
using System.Text;

namespace CafeGameEnvironmentAssistant.Core;

public static class CommandRunner
{
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? AppPaths.ExecutableDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    public static Task<CommandResult> RunPowerShellAsync(
        string command,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var escaped = command.Replace("\"", "\\\"");
        return RunAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{escaped}\"",
            workingDirectory,
            cancellationToken);
    }
}

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
