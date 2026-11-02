// In-app updates from this repository's GitHub Releases. The only network access in the app:
// it reads the latest release, downloads one zip over HTTPS from GitHub hosts only, checks its SHA-256,
// and stages it next to the install folder. Applying happens on restart (see Updater.Apply).
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nordlys.Core;

public sealed record ReleaseInfo(Version Version, string Tag, string Notes, string PageUrl, Uri ZipUrl, Uri ShaUrl, long ZipSize);

public static partial class Updates
{
    public const string Owner = "sentencedtolife";
    public const string Repo = "PersonalLearningToolCode";
    public static readonly Uri LatestReleaseApi = new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    /// <summary>Hosts GitHub serves release data and downloads from. Anything else is refused, including redirects.</summary>
    private static readonly string[] AllowedHosts = ["api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    public static bool IsAllowed(Uri u) => u.Scheme == Uri.UriSchemeHttps && AllowedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);

    public static string ZipName(Version v) => $"Nordlys-{v.ToString(3)}-win-x64.zip";

    /// <summary>"v0.3.1", "0.3.1", "0.3.1+abc" -> 0.3.1. Null if it isn't a version.</summary>
    public static Version? ParseVersion(string? s)
    {
        if (s == null) return null;
        var m = VersionRe().Match(s.Trim());
        return m.Success && Version.TryParse(m.Groups[1].Value, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : null;
    }

    /// <summary>The release in the GitHub API's "latest release" JSON, or null if it has no usable app zip and checksum.</summary>
    public static ReleaseInfo? ParseLatest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) return null;
        if (r.TryGetProperty("prerelease", out var p) && p.GetBoolean()) return null;
        var tag = r.GetProperty("tag_name").GetString() ?? "";
        if (ParseVersion(tag) is not { } version) return null;
        Uri? zip = null, sha = null;
        long size = 0;
        foreach (var a in r.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString();
            var url = a.GetProperty("browser_download_url").GetString();
            if (url == null || !Uri.TryCreate(url, UriKind.Absolute, out var u) || !IsAllowed(u)) continue;
            if (name == ZipName(version)) { zip = u; size = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0; }
            else if (name == ZipName(version) + ".sha256") sha = u;
        }
        if (zip == null || sha == null) return null;
        var notes = r.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        var page = r.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
        return new ReleaseInfo(version, tag, notes, page, zip, sha, size);
    }

    /// <summary>The first 64-character hex token of a .sha256 file ("hash  filename" or just the hash).</summary>
    public static string? ParseSha(string text) => ShaRe().Match(text) is { Success: true } m ? m.Value.ToLowerInvariant() : null;

    public static string Sha256(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }

    /// <summary>Extracts the app zip into dest. Refuses entries that would land outside dest, and zips without Nordlys.exe.</summary>
    public static void ExtractSafely(string zipPath, string dest)
    {
        var root = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(zipPath);
        if (!zip.Entries.Any(e => e.FullName.Equals("Nordlys.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The update does not contain Nordlys.exe.");
        Directory.CreateDirectory(root);
        foreach (var e in zip.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, e.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Unsafe path in the update: {e.FullName}");
            if (e.FullName.EndsWith('/') || e.FullName.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, overwrite: true);
        }
    }

    [GeneratedRegex(@"^v?(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex VersionRe();

    [GeneratedRegex(@"\b[0-9a-fA-F]{64}\b")]
    private static partial Regex ShaRe();
}

/// <summary>Talks to GitHub. The HttpMessageHandler is replaceable for tests.</summary>
public sealed class UpdateClient(HttpMessageHandler? handler = null)
{
    private readonly HttpClient http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>GET with redirects followed by hand, so every hop is checked against the allowed GitHub hosts.</summary>
    private async Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken ct, bool stream = false)
    {
        for (var hop = 0; hop < 6; hop++)
        {
            if (!Updates.IsAllowed(url)) throw new HttpRequestException($"Refusing to download from {url.Host}.");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd($"Nordlys/{AppState.AppVersion}");
            if (url.Host == "api.github.com") req.Headers.Accept.ParseAdd("application/vnd.github+json");
            var res = await http.SendAsync(req, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, ct);
            if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc)
            {
                url = loc.IsAbsoluteUri ? loc : new Uri(url, loc);
                res.Dispose();
                continue;
            }
            return res;
        }
        throw new HttpRequestException("Too many redirects.");
    }

    /// <summary>The latest release if it is newer than current; null if up to date or there is no usable release.</summary>
    public async Task<ReleaseInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var res = await GetAsync(Updates.LatestReleaseApi, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null; // no releases yet
        res.EnsureSuccessStatusCode();
        var release = Updates.ParseLatest(await res.Content.ReadAsStringAsync(ct));
        return release != null && release.Version > current ? release : null;
    }

    /// <summary>Downloads, checks the SHA-256 and extracts the release into stagingDir. Returns the folder with the new app.</summary>
    public async Task<string> DownloadAsync(ReleaseInfo r, string stagingDir, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(stagingDir);
        string expected;
        using (var shaRes = await GetAsync(r.ShaUrl, ct))
        {
            shaRes.EnsureSuccessStatusCode();
            expected = Updates.ParseSha(await shaRes.Content.ReadAsStringAsync(ct)) ?? throw new InvalidDataException("The checksum file is not readable.");
        }

        var zipPath = Path.Combine(stagingDir, Updates.ZipName(r.Version));
        using (var res = await GetAsync(r.ZipUrl, ct, stream: true))
        {
            res.EnsureSuccessStatusCode();
            var total = res.Content.Headers.ContentLength ?? r.ZipSize;
            await using var src = await res.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(zipPath);
            var buffer = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress?.Report((double)done / total);
            }
        }

        var actual = Updates.Sha256(zipPath);
        if (actual != expected)
        {
            File.Delete(zipPath);
            throw new InvalidDataException("The download is damaged (its checksum does not match). Nothing was changed.");
        }
        var appDir = Path.Combine(stagingDir, "app");
        if (Directory.Exists(appDir)) Directory.Delete(appDir, recursive: true);
        await Task.Run(() => Updates.ExtractSafely(zipPath, appDir), ct);
        File.Delete(zipPath);
        return appDir;
    }
}

/// <summary>Replaces the installed app with a staged one. Run from the staged copy, after the old app has exited.</summary>
public static class Updater
{
    /// <summary>Files from the old install that the new one keeps (written by the installer, not part of the build).</summary>
    public static readonly string[] Keep = ["uninstall.exe"];

    /// <summary>
    /// Swaps installDir for stagedDir: renames the old folder aside, copies the new files in, carries over Keep files,
    /// then deletes the old folder. On any failure the old folder is put back, so the app is never left half-updated.
    /// </summary>
    public static void Apply(string stagedDir, string installDir, TimeSpan? waitForUnlock = null)
    {
        installDir = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar);
        var old = $"{installDir}-old-{DateTime.Now:yyyyMMddHHmmss}";
        var deadline = DateTime.Now + (waitForUnlock ?? TimeSpan.FromSeconds(15));
        // The old process may still be exiting; its files stay locked for a moment.
        for (; ; )
        {
            try { Directory.Move(installDir, old); break; }
            catch (IOException) when (DateTime.Now < deadline) { Thread.Sleep(250); }
            catch (UnauthorizedAccessException) when (DateTime.Now < deadline) { Thread.Sleep(250); }
        }
        try
        {
            CopyDir(stagedDir, installDir);
            foreach (var k in Keep)
                if (File.Exists(Path.Combine(old, k))) File.Copy(Path.Combine(old, k), Path.Combine(installDir, k), overwrite: true);
        }
        catch
        {
            if (Directory.Exists(installDir)) Directory.Delete(installDir, recursive: true);
            Directory.Move(old, installDir);
            throw;
        }
        try { Directory.Delete(old, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
    }
}
