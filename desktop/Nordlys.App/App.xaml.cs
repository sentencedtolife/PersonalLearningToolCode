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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnCrash;
        Menus.RegisterTextBoxMenus();

        // --data <folder> uses another data folder (for testing). Default: %APPDATA%\app.nordlys.
        var i = Array.IndexOf(e.Args, "--data");
        var root = i >= 0 && i + 1 < e.Args.Length ? Path.GetFullPath(e.Args[i + 1]) : FileStore.DefaultRoot;
        Directory.CreateDirectory(root);

        State = await AppState.LoadAsync(new FileStore(root));
        ThemeManager.Apply(State.Settings);
        var window = new MainWindow(State);
        MainWindow = window;
        window.Show();
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
