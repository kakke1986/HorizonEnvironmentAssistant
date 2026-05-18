using System.Reflection;
using System.Security.Cryptography;

namespace CafeGameEnvironmentAssistant.Core;

public static class EmbeddedPayloadHelper
{
    private const string ResourcePrefix = "payload/";

    public static void ExtractAll(string outputDirectory)
    {
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)))
        {
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                continue;
            }

            var relativePath = resourceName[ResourcePrefix.Length..]
                .Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.Combine(outputDirectory, relativePath);
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
