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
        if (!process.Start())
        {
            throw new InvalidOperationException($"无法启动命令：{fileName}");
        }

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);

            return new CommandResult(
                process.ExitCode,
                stdoutTask.Result.Trim(),
                stderrTask.Result.Trim());
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
    }

    public static Task<CommandResult> RunPowerShellAsync(
        string command,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var bootstrapCommand =
            "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "$OutputEncoding = [Console]::OutputEncoding; " +
            command;
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(bootstrapCommand));
        return RunAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            workingDirectory,
            cancellationToken);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore cleanup failures while unwinding a cancelled command.
        }
    }
}

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
