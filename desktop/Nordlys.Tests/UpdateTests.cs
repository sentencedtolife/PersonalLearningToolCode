using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Nordlys.Core;

namespace Nordlys.Tests;

public class UpdateTests
{
    private static string ReleaseJson(string tag = "v0.3.1", bool draft = false, bool prerelease = false, string zipUrlHost = "github.com") => $$"""
        {
          "tag_name": "{{tag}}", "draft": {{draft.ToString().ToLower()}}, "prerelease": {{prerelease.ToString().ToLower()}},
          "html_url": "https://github.com/sentencedtolife/PersonalLearningToolCode/releases/tag/{{tag}}",
          "body": "- Better buttons",
          "assets": [
            { "name": "Nordlys-0.3.1-win-x64.zip", "size": 1234, "browser_download_url": "https://{{zipUrlHost}}/sentencedtolife/PersonalLearningToolCode/releases/download/{{tag}}/Nordlys-0.3.1-win-x64.zip" },
            { "name": "Nordlys-0.3.1-win-x64.zip.sha256", "size": 90, "browser_download_url": "https://github.com/sentencedtolife/PersonalLearningToolCode/releases/download/{{tag}}/Nordlys-0.3.1-win-x64.zip.sha256" },
            { "name": "Nordlys-0.3.1-setup.exe", "size": 9, "browser_download_url": "https://github.com/x/y/z.exe" }
          ]
        }
        """;

    [Fact]
    public void Versions()
    {
        Assert.Equal(new Version(0, 3, 1), Updates.ParseVersion("v0.3.1"));
        Assert.Equal(new Version(0, 3, 1), Updates.ParseVersion("0.3.1+25ac69a"));
        Assert.Equal(new Version(1, 2, 0), Updates.ParseVersion("1.2"));
        Assert.Null(Updates.ParseVersion("latest"));
        Assert.True(Updates.ParseVersion("0.10.0") > Updates.ParseVersion("0.9.9"));
    }

    [Fact]
    public void Parses_the_latest_release()
    {
        var r = Updates.ParseLatest(ReleaseJson())!;
        Assert.Equal(new Version(0, 3, 1), r.Version);
        Assert.EndsWith("Nordlys-0.3.1-win-x64.zip", r.ZipUrl.ToString());
        Assert.EndsWith(".zip.sha256", r.ShaUrl.ToString());
        Assert.Equal("- Better buttons", r.Notes);
    }

    [Fact]
    public void Ignores_drafts_prereleases_and_foreign_hosts()
    {
        Assert.Null(Updates.ParseLatest(ReleaseJson(draft: true)));
        Assert.Null(Updates.ParseLatest(ReleaseJson(prerelease: true)));
        Assert.Null(Updates.ParseLatest(ReleaseJson(zipUrlHost: "evil.example.com")));
        Assert.False(Updates.IsAllowed(new Uri("http://github.com/x")));
        Assert.True(Updates.IsAllowed(new Uri("https://objects.githubusercontent.com/x")));
    }

    [Fact]
    public void Reads_sha_files() =>
        Assert.Equal(new string('a', 64), Updates.ParseSha($"{new string('A', 64)}  Nordlys-0.3.1-win-x64.zip\n"));

    /// <summary>Serves fixed responses by URL; anything unknown is 404.</summary>
    private sealed class FakeGitHub(Dictionary<string, Func<HttpResponseMessage>> routes) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requested.Add(request.RequestUri!.ToString());
            return Task.FromResult(routes.TryGetValue(request.RequestUri!.ToString(), out var f) ? f() : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static byte[] AppZip(Action<ZipArchive>? extra = null)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("Nordlys.exe").Open())) w.Write("new exe");
            using (var w = new StreamWriter(zip.CreateEntry("runtimes/x.dll").Open())) w.Write("dll");
            extra?.Invoke(zip);
        }
        return ms.ToArray();
    }

    private static HttpResponseMessage Bytes(byte[] b) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(b) };
    private static HttpResponseMessage Text(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8) };
    private static HttpResponseMessage Redirect(string to) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(to) } };

    private static string Temp() => Directory.CreateTempSubdirectory("nordlys-upd-").FullName;

    [Fact]
    public async Task Check_and_download_with_redirect_and_checksum()
    {
        var zip = AppZip();
        var sha = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        var r = Updates.ParseLatest(ReleaseJson())!;
        var fake = new FakeGitHub(new()
        {
            [Updates.LatestReleaseApi.ToString()] = () => Text(ReleaseJson()),
            [r.ShaUrl.ToString()] = () => Text($"{sha}  Nordlys-0.3.1-win-x64.zip"),
            [r.ZipUrl.ToString()] = () => Redirect("https://objects.githubusercontent.com/blob/zip"),
            ["https://objects.githubusercontent.com/blob/zip"] = () => Bytes(zip),
        });
        var client = new UpdateClient(fake);
        Assert.Null(await client.CheckAsync(new Version(0, 3, 1)));
        var found = await client.CheckAsync(new Version(0, 3, 0));
        Assert.NotNull(found);
        var staging = Temp();
        var progress = new List<double>();
        var app = await client.DownloadAsync(found!, staging, new Progress<double>(progress.Add));
        Assert.Equal("new exe", File.ReadAllText(Path.Combine(app, "Nordlys.exe")));
        Assert.True(File.Exists(Path.Combine(app, "runtimes", "x.dll")));
    }

    [Fact]
    public async Task Refuses_redirects_away_from_github()
    {
        var r = Updates.ParseLatest(ReleaseJson())!;
        var fake = new FakeGitHub(new()
        {
            [r.ShaUrl.ToString()] = () => Redirect("https://evil.example.com/sha"),
        });
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new UpdateClient(fake).DownloadAsync(r, Temp()));
        Assert.Contains("evil.example.com", ex.Message);
        Assert.DoesNotContain(fake.Requested, u => u.Contains("evil"));
    }

    [Fact]
    public async Task Refuses_a_damaged_download()
    {
        var r = Updates.ParseLatest(ReleaseJson())!;
        var fake = new FakeGitHub(new()
        {
            [r.ShaUrl.ToString()] = () => Text(new string('0', 64)),
            [r.ZipUrl.ToString()] = () => Bytes(AppZip()),
        });
        var staging = Temp();
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateClient(fake).DownloadAsync(r, staging));
        Assert.False(Directory.Exists(Path.Combine(staging, "app")));
    }

    [Fact]
    public void Refuses_zips_that_escape_the_folder_or_lack_the_app()
    {
        var dir = Temp();
        var evil = Path.Combine(dir, "evil.zip");
        File.WriteAllBytes(evil, AppZip(z => { using var w = new StreamWriter(z.CreateEntry("../../outside.txt").Open()); w.Write("x"); }));
        Assert.Throws<InvalidDataException>(() => Updates.ExtractSafely(evil, Path.Combine(dir, "out")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(dir)!, "outside.txt")));

        var empty = Path.Combine(dir, "empty.zip");
        using (var z = ZipFile.Open(empty, ZipArchiveMode.Create)) z.CreateEntry("readme.txt");
        Assert.Throws<InvalidDataException>(() => Updates.ExtractSafely(empty, Path.Combine(dir, "out2")));
    }

    [Fact]
    public void Apply_swaps_the_folder_and_keeps_the_uninstaller()
    {
        var root = Temp();
        var install = Path.Combine(root, "Nordlys");
        var staged = Path.Combine(root, "staged");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Nordlys.exe"), "old");
        File.WriteAllText(Path.Combine(install, "obsolete.dll"), "old");
        File.WriteAllText(Path.Combine(install, "uninstall.exe"), "uninstaller");
        Directory.CreateDirectory(Path.Combine(staged, "runtimes"));
        File.WriteAllText(Path.Combine(staged, "Nordlys.exe"), "new");
        File.WriteAllText(Path.Combine(staged, "runtimes", "x.dll"), "new");

        Updater.Apply(staged, install);

        Assert.Equal("new", File.ReadAllText(Path.Combine(install, "Nordlys.exe")));
        Assert.True(File.Exists(Path.Combine(install, "runtimes", "x.dll")));
        Assert.Equal("uninstaller", File.ReadAllText(Path.Combine(install, "uninstall.exe")));
        Assert.False(File.Exists(Path.Combine(install, "obsolete.dll")));
        Assert.Empty(Directory.GetDirectories(root, "Nordlys-old-*"));
    }

    [Fact]
    public void Apply_puts_the_old_app_back_if_copying_fails()
    {
        var root = Temp();
        var install = Path.Combine(root, "Nordlys");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Nordlys.exe"), "old");
        var missing = Path.Combine(root, "does-not-exist");

        Assert.ThrowsAny<IOException>(() => Updater.Apply(missing, install));

        Assert.Equal("old", File.ReadAllText(Path.Combine(install, "Nordlys.exe")));
        Assert.Empty(Directory.GetDirectories(root, "Nordlys-old-*"));
    }
}
