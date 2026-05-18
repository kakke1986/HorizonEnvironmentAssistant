using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CafeGameEnvironmentAssistant.Core;

public static class StorePackageClient
{
    private const string ParserEndpoint = "https://store.rg-adguard.net/api/GetFiles";
    private const string GamingServicesProductId = "9MWPM2CQNLHN";
    private const string XboxIdentityProviderProductId = "9WZDNCRD1HKW";
    private const int MaxDownloadAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<IReadOnlyList<RemotePackageInfo>> ResolveRequiredPackagesAsync()
    {
        var gamingServicesEntries = await ResolveProductEntriesAsync(GamingServicesProductId);
        var xboxIdentityEntries = await ResolveProductEntriesAsync(XboxIdentityProviderProductId);

        return
        [
            SelectNewest(
                gamingServicesEntries,
                "Microsoft.VCLibs.x64.appx",
                name => name.StartsWith("Microsoft.VCLibs.140.00_", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("_x64__", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".appx", StringComparison.OrdinalIgnoreCase)),
            SelectNewest(
                gamingServicesEntries,
                "Microsoft.NET.Native.Framework.x64.appx",
                name => name.StartsWith("Microsoft.NET.Native.Framework.2.2_", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("_x64__", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".appx", StringComparison.OrdinalIgnoreCase)),
            SelectNewest(
                gamingServicesEntries,
                "Microsoft.NET.Native.Runtime.x64.appx",
                name => name.StartsWith("Microsoft.NET.Native.Runtime.2.2_", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("_x64__", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".appx", StringComparison.OrdinalIgnoreCase)),
            SelectNewest(
                xboxIdentityEntries,
                "XboxIdentityProvider.appxbundle",
                name => name.StartsWith("Microsoft.XboxIdentityProvider_", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase)),
            SelectNewest(
                gamingServicesEntries,
                "GamingServices.msixbundle",
                name => name.StartsWith("Microsoft.GamingServices_", StringComparison.OrdinalIgnoreCase)
                    && (name.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase)))
        ];
    }

    public static async Task DownloadPackageAsync(
        RemotePackageInfo package,
        string destinationPath,
        Action<string>? statusChanged = null,
        Action<DownloadProgress>? progressChanged = null)
    {
        var tempPath = $"{destinationPath}.download";
        try
        {
            var currentPackage = package;

            for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
            {
                try
                {
                    await DownloadPackageChunkAsync(currentPackage.DownloadUrl, tempPath, progressChanged);

                    var sha1 = await ComputeSha1Async(tempPath);
                    if (!string.Equals(sha1, currentPackage.Sha1, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"下载校验失败：{currentPackage.TargetFileName}");
                    }

                    File.Move(tempPath, destinationPath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (attempt < MaxDownloadAttempts && IsRetryableDownloadException(ex))
                {
                    LogHelper.Write($"下载中断，准备重试：{currentPackage.TargetFileName}，第 {attempt} 次失败。");
                    statusChanged?.Invoke($"下载中断，准备重试第 {attempt + 1} 次。");
                    currentPackage = await RefreshPackageInfoAsync(currentPackage.TargetFileName);
                    await Task.Delay(RetryDelay);
                }
            }

            throw new InvalidOperationException($"下载失败：{package.TargetFileName}");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async Task<RemotePackageInfo> RefreshPackageInfoAsync(string targetFileName)
    {
        var refreshedPackages = await ResolveRequiredPackagesAsync();
        var refreshedPackage = refreshedPackages.FirstOrDefault(
            package => string.Equals(package.TargetFileName, targetFileName, StringComparison.OrdinalIgnoreCase));

        return refreshedPackage
            ?? throw new InvalidOperationException($"刷新下载地址失败：{targetFileName}");
    }

    private static async Task DownloadPackageChunkAsync(
        string downloadUrl,
        string tempPath,
        Action<DownloadProgress>? progressChanged)
    {
        var existingLength = File.Exists(tempPath)
            ? new FileInfo(tempPath).Length
            : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        if (existingLength > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingLength, null);
        }

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (existingLength > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            File.Delete(tempPath);
            existingLength = 0;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"下载响应失败：{(int)response.StatusCode} ({response.ReasonPhrase})",
                null,
                response.StatusCode);
        }

        var fileMode = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent
            ? FileMode.Append
            : FileMode.Create;

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = new FileStream(
            tempPath,
            fileMode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 128,
            useAsync: true);

        var totalBytes = GetTotalBytes(response, existingLength);
        var transferredBytes = existingLength;
        var buffer = new byte[1024 * 128];

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, bytesRead));
            transferredBytes += bytesRead;
            progressChanged?.Invoke(new DownloadProgress(transferredBytes, totalBytes));
        }
    }

    private static async Task<IReadOnlyList<StoreFileEntry>> ResolveProductEntriesAsync(string productId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ParserEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("type", "ProductId"),
                new KeyValuePair<string, string>("url", productId),
                new KeyValuePair<string, string>("ring", "Retail"),
                new KeyValuePair<string, string>("lang", "en-US")
            ])
        };
        request.Headers.Referrer = new Uri("https://store.rg-adguard.net/");

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var entries = ParseEntries(html);
        if (entries.Count == 0)
        {
            throw new InvalidOperationException($"未解析到产品 {productId} 的商店文件。");
        }

        return entries;
    }

    private static IReadOnlyList<StoreFileEntry> ParseEntries(string html)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
        return Regex.Matches(html, @"<tr[^>]*>(?<row>.*?)</tr>", options)
            .Select(match => match.Groups["row"].Value)
            .Select(ParseRow)
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToList();
    }

    private static StoreFileEntry? ParseRow(string rowHtml)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
        var linkMatch = Regex.Match(
            rowHtml,
            """<a\s+href="(?<url>[^"]+)"[^>]*>(?<name>[^<]+)</a>""",
            options);
        if (!linkMatch.Success)
        {
            return null;
        }

        var cellMatches = Regex.Matches(rowHtml, @"<td[^>]*>(?<value>.*?)</td>", options);
        if (cellMatches.Count < 4)
        {
            return null;
        }

        return new StoreFileEntry(
            WebUtility.HtmlDecode(linkMatch.Groups["name"].Value.Trim()),
            WebUtility.HtmlDecode(linkMatch.Groups["url"].Value.Trim()),
            StripHtml(cellMatches[2].Groups["value"].Value),
            StripHtml(cellMatches[3].Groups["value"].Value));
    }

    private static string StripHtml(string html)
    {
        var withoutTags = Regex.Replace(html, "<.*?>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(withoutTags.Trim());
    }

    private static RemotePackageInfo SelectNewest(
        IEnumerable<StoreFileEntry> entries,
        string targetFileName,
        Func<string, bool> predicate)
    {
        var selected = entries
            .Where(entry => predicate(entry.FileName))
            .OrderByDescending(entry => ExtractVersion(entry.FileName))
            .FirstOrDefault();

        if (selected is null)
        {
            throw new InvalidOperationException($"未找到所需商店包：{targetFileName}");
        }

        return new RemotePackageInfo(
            targetFileName,
            selected.FileName,
            selected.DownloadUrl,
            selected.Sha1,
            selected.Size);
    }

    private static Version ExtractVersion(string fileName)
    {
        var match = Regex.Match(fileName, @"_(?<version>\d+(?:\.\d+){1,3})_", RegexOptions.IgnoreCase);
        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version)
            ? version
            : new Version(0, 0);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(20)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Mozilla", "5.0"));
        return client;
    }

    private static async Task<string> ComputeSha1Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha1 = SHA1.Create();
        var hash = await sha1.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsRetryableDownloadException(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode is null
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }

        return ex is IOException or TaskCanceledException;
    }

    private static long? GetTotalBytes(HttpResponseMessage response, long existingLength)
    {
        if (response.Content.Headers.ContentRange?.Length is long contentRangeLength)
        {
            return contentRangeLength;
        }

        if (response.Content.Headers.ContentLength is long contentLength)
        {
            return existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent
                ? existingLength + contentLength
                : contentLength;
        }

        return null;
    }

    private sealed record StoreFileEntry(string FileName, string DownloadUrl, string Sha1, string Size);
}

public sealed record RemotePackageInfo(
    string TargetFileName,
    string SourceFileName,
    string DownloadUrl,
    string Sha1,
    string Size);

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public int? Percent => TotalBytes is > 0
        ? (int)Math.Clamp(BytesReceived * 100 / TotalBytes.Value, 0, 100)
        : null;
}
