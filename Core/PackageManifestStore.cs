using System.Text.Json;

namespace CafeGameEnvironmentAssistant.Core;

public static class PackageManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string ManifestPath =>
        Path.Combine(AppPaths.OfflinePackagesDirectory, "packages-manifest.json");

    public static async Task SaveAsync(PackageManifest manifest)
    {
        Directory.CreateDirectory(AppPaths.OfflinePackagesDirectory);
        var tempPath = $"{ManifestPath}.tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions);
            }

            File.Move(tempPath, ManifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static async Task<PackageManifest?> LoadAsync()
    {
        if (!File.Exists(ManifestPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(ManifestPath);
            return await JsonSerializer.DeserializeAsync<PackageManifest>(stream, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            LogHelper.Write($"读取 packages-manifest.json 失败：{ex.Message}");
            return null;
        }
    }

    public static bool HasOnlineChanges(PackageManifest manifest, IEnumerable<RemotePackageInfo> onlinePackages)
    {
        var onlineLookup = onlinePackages.ToDictionary(
            package => package.TargetFileName,
            StringComparer.OrdinalIgnoreCase);

        foreach (var localPackage in manifest.Packages)
        {
            if (!onlineLookup.TryGetValue(localPackage.TargetFileName, out var onlinePackage))
            {
                return true;
            }

            if (!Matches(localPackage, onlinePackage))
            {
                return true;
            }
        }

        return manifest.Packages.Count != onlineLookup.Count;
    }

    public static bool Matches(PackageManifestEntry localPackage, RemotePackageInfo onlinePackage)
    {
        return string.Equals(localPackage.SourceFileName, onlinePackage.SourceFileName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(localPackage.Sha1, onlinePackage.Sha1, StringComparison.OrdinalIgnoreCase)
            && localPackage.Size == onlinePackage.Size;
    }
}

public sealed record PackageManifest(DateTimeOffset DownloadedAtUtc, IReadOnlyList<PackageManifestEntry> Packages);

public sealed record PackageManifestEntry(
    string TargetFileName,
    string SourceFileName,
    string Sha1,
    string Size)
{
    public static PackageManifestEntry FromRemotePackage(RemotePackageInfo package)
    {
        return new PackageManifestEntry(
            package.TargetFileName,
            package.SourceFileName,
            package.Sha1,
            package.Size);
    }
}
