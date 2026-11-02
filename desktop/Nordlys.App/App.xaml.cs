using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Nordlys.App.Shell;
using Nordlys.App.Theme;
using Nordlys.Core;
using Nordlys.Core.Storage;

namespace Nordlys.App;

public partial class App : Application
{
    public static AppState State { get; private set; } = null!;
    /// <summary>Milliseconds from process start to the first screen being shown (Settings shows it).</summary>
    public static double StartupMs { get; set; }
    /// <summary>The --data folder, if one was given (kept across an update restart).</summary>
    public static string? DataOverride { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnCrash;

        // Started from a downloaded update: replace the installed app, then start it again.
        if (e.Args.Length >= 4 && e.Args[0] == "--apply-update")
        {
            await ApplyUpdateAsync(e.Args[1], int.Parse(e.Args[2]), e.Args[3], Arg(e.Args, "--data"));
            return;
        }

        Menus.RegisterTextBoxMenus();

        // --data <folder> uses another data folder (for testing). Default: %APPDATA%\app.nordlys.
        var i = Array.IndexOf(e.Args, "--data");
        DataOverride = i >= 0 && i + 1 < e.Args.Length ? Path.GetFullPath(e.Args[i + 1]) : null;
        var root = DataOverride ?? FileStore.DefaultRoot;
        Directory.CreateDirectory(root);

        State = await AppState.LoadAsync(new FileStore(root));
        if (Arg(e.Args, "--updated") is { } v) { State.Notice = $"Nordlys is updated to {v}."; UpdateService.CleanUpStagingLater(); }
        else if (Arg(e.Args, "--update-failed") is { } why) { State.Notice = $"The update could not be installed, so Nordlys is unchanged: {why}"; UpdateService.CleanUpStagingLater(); }
        ThemeManager.Apply(State.Settings);
        var window = new MainWindow(State);
        MainWindow = window;
        window.Show();
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>A small "Updating…" window while the folders are swapped, then the installed app starts again.</summary>
    private async Task ApplyUpdateAsync(string installDir, int oldPid, string version, string? data)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var w = new Window
        {
            Title = "Nordlys", Width = 360, Height = 120, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(Nord.N0),
            Content = new System.Windows.Controls.TextBlock
            {
                Text = $"Updating Nordlys to {version}…", Foreground = new System.Windows.Media.SolidColorBrush(Nord.N6), FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        w.Show();
        var restartArgs = await Task.Run(() => UpdateService.ApplyFromStaged(installDir, oldPid, version));
        var psi = new ProcessStartInfo(Path.Combine(installDir, "Nordlys.exe")) { UseShellExecute = false };
        foreach (var a in restartArgs) psi.ArgumentList.Add(a);
        if (data != null) { psi.ArgumentList.Add("--data"); psi.ArgumentList.Add(data); }
        Process.Start(psi);
        Shutdown();
    }

    private static void OnCrash(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var root = State?.Store.Root ?? FileStore.DefaultRoot;
            File.AppendAllText(Path.Combine(root, "crash.log"), $"{DateTime.Now:O}\n{e.Exception}\n\n");
        }
        catch (IOException) { }
        Debug.WriteLine(e.Exception);
        MessageBox.Show($"Something went wrong: {e.Exception.Message}\n\nYour progress is saved. Details are in crash.log in the data folder.",
            "Nordlys", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
