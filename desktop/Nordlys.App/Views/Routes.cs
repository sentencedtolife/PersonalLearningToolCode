using System.Diagnostics;
using System.IO;
using System.Windows;
using Nordlys.App.Shell;
using Nordlys.Core;

namespace Nordlys.App.Views;

public static class Routes
{
    /// <summary>The shared app state (named Data because App is the WPF application class).</summary>
    public static AppState Data => Nordlys.App.App.State;
    private static MainWindow Main => (MainWindow)Application.Current.MainWindow;

    public static void Go(string hash) => Main.Nav.Navigate(hash);
    public static void Replace(string hash) => Main.Nav.Navigate(hash, replace: true);
    public static void Reload() => Main.Nav.Reload();

    public static View Resolve(Route r) => r.Section switch
    {
        "lesson" when r.Parts.Count >= 2 => LessonView.Create(r.Parts[1], r.Parts.Count > 2 && r.Parts[2] == "practice"),
        "library" => new LibraryView(),
        "map" => new MapView(r.Query.GetValueOrDefault("unit")),
        "review" => new ReviewView(),
        "ask" => new AskView(r.Query.GetValueOrDefault("unit"), r.Query.GetValueOrDefault("energy"), r.Query.GetValueOrDefault("request")),
        "import" => new ImportView(),
        "glossary" => new GlossaryView(r.Query.GetValueOrDefault("term")),
        "settings" => new SettingsView(),
        _ => new HomeView(),
    };

    /// <summary>Full path of a lesson file in the data folder.</summary>
    public static string? LessonFile(string id) => Data.Store.Root is { } root ? Path.Combine(root, "lessons", $"{id}.lesson.md") : null;

    public static void ShowInExplorer(string path)
    {
        if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>Opens a lesson file in the default editor for .md files, or Notepad.</summary>
    public static void OpenInEditor(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true }); }
    }
}
