using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HorizonEnvironmentAssistant.Launcher;

internal static class Program
{
    private const string ResourcePrefix = "payload/";
    private const string RuntimeDirectoryName = ".horizon-runtime";

    [STAThread]
    private static void Main()
    {
        try
        {
            if (!HasDotNet8DesktopRuntime())
            {
                MessageBox.Show(
                    "当前系统未检测到 .NET 8 Desktop Runtime。\r\n\r\n请先安装 .NET 8 Desktop Runtime 后再运行本程序。",
                    "地平线环境助手",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var launcherPath = Assembly.GetExecutingAssembly().Location;
            var launcherDirectory = Path.GetDirectoryName(launcherPath)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            var runtimeDirectory = Path.Combine(launcherDirectory, RuntimeDirectoryName);

            Directory.CreateDirectory(runtimeDirectory);
            Directory.CreateDirectory(Path.Combine(launcherDirectory, "OfflinePackages"));
            ExtractPayload(runtimeDirectory);

            var innerExecutable = Path.Combine(runtimeDirectory, "CafeGameEnvironmentAssistant.exe");
            if (!File.Exists(innerExecutable))
            {
                throw new FileNotFoundException("未找到主程序。", innerExecutable);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = innerExecutable,
                WorkingDirectory = runtimeDirectory,
                UseShellExecute = false
            };
            startInfo.EnvironmentVariables["HORIZON_APP_HOME"] = launcherDirectory;

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "启动失败。\r\n\r\n" + ex.Message,
                "地平线环境助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static bool HasDotNet8DesktopRuntime()
    {
        return HasDotNet8DesktopRuntimeInRegistry(RegistryView.Registry64)
            || HasDotNet8DesktopRuntimeInRegistry(RegistryView.Registry32)
            || HasDotNet8DesktopRuntimeInDirectory(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet",
                    "shared",
                    "Microsoft.WindowsDesktop.App"))
            || HasDotNet8DesktopRuntimeInDirectory(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "dotnet",
                    "shared",
                    "Microsoft.WindowsDesktop.App"));
    }

    private static bool HasDotNet8DesktopRuntimeInRegistry(RegistryView view)
    {
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var desktopRuntimeKey = localMachine.OpenSubKey(
                @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");

            return desktopRuntimeKey?.GetSubKeyNames()
                .Any(version => version.StartsWith("8.", StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasDotNet8DesktopRuntimeInDirectory(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                && Directory.EnumerateDirectories(directory)
                    .Select(Path.GetFileName)
                    .Any(version => !string.IsNullOrWhiteSpace(version)
                        && version.StartsWith("8.", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static void ExtractPayload(string runtimeDirectory)
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)))
        {
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                continue;
            }

            var relativePath = resourceName.Substring(ResourcePrefix.Length)
                .Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.Combine(runtimeDirectory, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            using var memoryStream = new MemoryStream();
            resourceStream.CopyTo(memoryStream);
            var payloadBytes = memoryStream.ToArray();

            if (File.Exists(targetPath) && FileMatches(targetPath, payloadBytes))
            {
                continue;
            }

            File.WriteAllBytes(targetPath, payloadBytes);
        }
    }

    private static bool FileMatches(string path, byte[] expectedBytes)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length != expectedBytes.LongLength)
        {
            return false;
        }

        using var expectedHash = SHA256.Create();
        using var actualHash = SHA256.Create();
        var expected = expectedHash.ComputeHash(expectedBytes);
        using var stream = File.OpenRead(path);
        var actual = actualHash.ComputeHash(stream);
        return expected.SequenceEqual(actual);
    }
}
