// In-app updates: check GitHub, download in the background, and swap the app folder on restart.
//
// Restart flow: the downloaded copy is started with --apply-update <installDir> <pid> <version>. It waits for this
// process to exit, swaps the folders (Updater.Apply, which rolls back on failure), updates the version shown in
// Settings > Apps, and starts the installed app again with --updated <version>.
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Nordlys.App.UI;
using Nordlys.Core;

namespace Nordlys.App.Shell;

public static class UpdateService
{
    public enum Phase { Idle, Checking, UpToDate, Available, Downloading, Ready, Failed }

    private static readonly UpdateClient Client = new();
    private static string? stagedApp;

    public static Phase State { get; private set; } = Phase.Idle;
    public static ReleaseInfo? Release { get; private set; }
    public static double Progress { get; private set; }
    public static string? Error { get; private set; }
    public static event Action? Changed;

    public static Version Current => Updates.ParseVersion(AppState.AppVersion) ?? new Version(0, 0, 0);
    public static string InstallDir => Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
    /// <summary>Only an installed copy (the installer leaves uninstall.exe next to it) can replace itself.</summary>
    public static bool IsInstalled => File.Exists(Path.Combine(InstallDir, "uninstall.exe"));
    /// <summary>Downloads go next to the install folder, never inside it, so the swap can rename the whole folder.</summary>
    public static string StagingRoot => Path.Combine(Path.GetDirectoryName(InstallDir)!, "Nordlys-update");

    private static void Set(Phase p, string? error = null)
    {
        State = p;
        Error = error;
        Changed?.Invoke();
    }

    private static string Friendly(Exception e) => e switch
    {
        HttpRequestException or TaskCanceledException => "Could not reach GitHub. Check your internet connection and try again.",
        _ => e.Message,
    };

    /// <summary>Once a day at start, if automatic checks are on. Silent when offline.</summary>
    public static void CheckOnStartIfDue(AppState app)
    {
        if (!app.Settings.AutoUpdateCheck || !IsInstalled) return;
        if (DateTime.TryParse(app.Settings.LastUpdateCheck, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last) && DateTime.UtcNow - last < TimeSpan.FromHours(20)) return;
        CheckAsync(app, manual: false).Forget();
    }

    public static async Task CheckAsync(AppState app, bool manual)
    {
        if (State is Phase.Checking or Phase.Downloading or Phase.Ready) return;
        Set(Phase.Checking);
        try
        {
            Release = await Client.CheckAsync(Current);
            app.Settings.LastUpdateCheck = DateTime.UtcNow.ToString("O");
            await app.SaveSettingsAsync();
            Set(Release != null ? Phase.Available : Phase.UpToDate);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException or System.Text.Json.JsonException or IOException)
        {
            // An automatic check never bothers anyone with a network error.
            Set(manual ? Phase.Failed : Phase.Idle, manual ? Friendly(e) : null);
        }
    }

    public static async Task DownloadAsync()
    {
        if (Release is not { } r || State is Phase.Downloading or Phase.Ready) return;
        if (!IsInstalled)
        {
            Set(Phase.Failed, "Updates install only into the installed app, not a development build.");
            return;
        }
        Progress = 0;
        Set(Phase.Downloading);
        try
        {
            var progress = new Progress<double>(p => { Progress = p; Changed?.Invoke(); });
            stagedApp = await Client.DownloadAsync(r, Path.Combine(StagingRoot, r.Version.ToString(3)), progress);
            Set(Phase.Ready);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Set(Phase.Failed, Friendly(e));
        }
    }

    /// <summary>Starts the downloaded copy to apply the update, then closes this window (which saves everything first).</summary>
    public static void RestartToUpdate()
    {
        if (State != Phase.Ready || stagedApp == null || Release == null) return;
        var psi = new ProcessStartInfo(Path.Combine(stagedApp, "Nordlys.exe")) { UseShellExecute = false };
        foreach (var a in new[] { "--apply-update", InstallDir, Environment.ProcessId.ToString(), Release.Version.ToString(3) }) psi.ArgumentList.Add(a);
        // A test data folder (--data) carries through the restart.
        if (App.DataOverride is { } data) { psi.ArgumentList.Add("--data"); psi.ArgumentList.Add(data); }
        Process.Start(psi);
        Application.Current.MainWindow?.Close();
    }

    /// <summary>Runs in the downloaded copy. Returns the arguments to restart the installed app with.</summary>
    public static string[] ApplyFromStaged(string installDir, int oldPid, string version)
    {
        try
        {
            try { using var old = Process.GetProcessById(oldPid); old.WaitForExit(30_000); } catch (ArgumentException) { /* already gone */ }
            Updater.Apply(InstallDir, installDir);
            // The version in Settings > Apps, only if that entry is this installation.
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Nordlys", writable: true);
            var location = (key?.GetValue("InstallLocation") as string)?.Trim('"').TrimEnd('\\');
            if (key != null && string.Equals(location, installDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) key.SetValue("DisplayVersion", version);
            return ["--updated", version];
        }
        catch (Exception e)
        {
            return ["--update-failed", e.Message];
        }
    }

    /// <summary>After an update (or a cancelled one), remove the downloaded copies. Best effort, a few seconds later.</summary>
    public static void CleanUpStagingLater()
    {
        if (!Directory.Exists(StagingRoot)) return;
        Task.Run(async () =>
        {
            for (var i = 0; i < 10 && Directory.Exists(StagingRoot); i++)
            {
                await Task.Delay(1500);
                try { Directory.Delete(StagingRoot, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        });
    }
}
