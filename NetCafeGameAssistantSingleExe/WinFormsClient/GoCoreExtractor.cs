using System.Reflection;

namespace NetCafeGameAssistant;

public static class GoCoreExtractor
{
    public const string AppDataDirectory = @"C:\ProgramData\NetCafeGameAssistant";
    public const string LogDirectory = AppDataDirectory + @"\Logs";
    public const string LatestLogPath = LogDirectory + @"\latest.log";

    private const string ResourceName = "Tools.GoRepairCore.exe";

    public static string Extract()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LogDirectory);

        var targetPath = Path.Combine(AppDataDirectory, "GoRepairCore.exe");
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("未找到嵌入的 GoRepairCore.exe，请先运行 build.bat。");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != bytes.LongLength)
        {
            File.WriteAllBytes(targetPath, bytes);
        }

        return targetPath;
    }
}
