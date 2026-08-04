using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace NetVsMcp.Broker.Services;

public record UpdateInfo(string Version, string MsiDownloadUrl, string ReleasePageUrl);

public class UpdateCheckService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/Alexander-Swan/netvs-mcp/releases?per_page=10";
    private const string GitHubReleasesUrl = "https://github.com/Alexander-Swan/netvs-mcp/releases/latest";

    /// <param name="currentVersion">The running broker version, e.g. "0.1.8" or "0.1.8-dev".</param>
    /// <param name="includeDevVersions">
    /// When <c>false</c>, only stable (non-prerelease) GitHub releases are considered. When <c>true</c>,
    /// dev/alpha/beta/etc. releases are considered too, and the newest overall release wins regardless of channel.
    /// </param>
    /// <param name="ignoredVersion">
    /// A version the user previously chose to ignore. That specific version is skipped even if it would
    /// otherwise be the newest available; a different (e.g. later) release still surfaces normally.
    /// </param>
    public async Task<UpdateInfo?> CheckAsync(
        string currentVersion,
        bool includeDevVersions = false,
        string? ignoredVersion = null,
        CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NetVsMcp-Broker");
            http.Timeout = TimeSpan.FromSeconds(15);

            var json = await http.GetStringAsync(GitHubApiUrl, ct);
            using var doc = JsonDocument.Parse(json);

            JsonElement? bestRelease = null;
            string? bestVersion = null;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean())
                    continue;

                var isPrerelease = release.TryGetProperty("prerelease", out var prereleaseProp) && prereleaseProp.GetBoolean();
                if (isPrerelease && !includeDevVersions)
                    continue;

                var tagVersion = (release.GetProperty("tag_name").GetString() ?? string.Empty).TrimStart('v');
                if (bestVersion is null || IsNewer(tagVersion, bestVersion))
                {
                    bestVersion = tagVersion;
                    bestRelease = release;
                }
            }

            if (bestRelease is null || bestVersion is null || !IsNewer(bestVersion, currentVersion))
                return null;

            if (!string.IsNullOrEmpty(ignoredVersion) &&
                string.Equals(bestVersion, ignoredVersion, StringComparison.OrdinalIgnoreCase))
                return null;

            var root = bestRelease.Value;
            var htmlUrl = root.GetProperty("html_url").GetString() ?? GitHubReleasesUrl;

            string? msiUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (name.StartsWith("NetVsMcp.Broker", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    msiUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            return msiUrl is null ? null : new UpdateInfo(bestVersion, msiUrl, htmlUrl);
        }
        catch
        {
            return null;
        }
    }

    public async Task DownloadAndInstallAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NetVsMcp-Broker");

        var tempPath = Path.Combine(Path.GetTempPath(), $"NetVsMcp.Broker-{update.Version}.msi");

        using (var response = await http.GetAsync(update.MsiDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1L;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(tempPath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                if (total > 0)
                    progress?.Report((int)(downloaded * 100 / total));
            }
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "msiexec",
            Arguments = $"/i \"{tempPath}\"",
            UseShellExecute = true
        });
    }

    private static bool IsNewer(string releaseVersion, string currentVersion)
    {
        var relParts = releaseVersion.Split('-', 2);
        var curParts = currentVersion.Split('-', 2);

        if (!Version.TryParse(relParts[0], out var relVer) ||
            !Version.TryParse(curParts[0], out var curVer))
            return string.Compare(releaseVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;

        var cmp = relVer.CompareTo(curVer);
        if (cmp != 0)
            return cmp > 0;

        // Same base version: stable (no suffix) > any pre-release
        var relSuffix = relParts.Length > 1 ? relParts[1] : string.Empty;
        var curSuffix = curParts.Length > 1 ? curParts[1] : string.Empty;

        if (relSuffix == curSuffix) return false;
        if (relSuffix.Length == 0) return true;   // stable > pre-release
        if (curSuffix.Length == 0) return false;  // pre-release < stable
        return string.Compare(relSuffix, curSuffix, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
