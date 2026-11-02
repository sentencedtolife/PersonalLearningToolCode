// Library: lessons grouped by track, filterable by track and status. Each lesson has a full right-click menu.
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed partial class LibraryView : View
{
    private static readonly Dictionary<string, string> StatusLabel = new() { ["new"] = "New", ["read"] = "In progress", ["practiced"] = "Practised" };
    // Filters stay as they were while the app is open.
    private static string trackFilter = "all", statusFilter = "all";

    private readonly StackPanel filters = new() { Margin = new Thickness(0, 0, 0, 16) };
    private readonly StackPanel list = new();
    private readonly ScrollViewer root;

    public override string Title => "Library";
    public override FrameworkElement Root => root;

    public LibraryView()
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var ask = Ui.Btn("Ask Claude for a lesson", () => Go("#/ask"), "BtnFlat");
        DockPanel.SetDock(ask, Dock.Right);
        header.Children.Add(ask);
        header.Children.Add(Ui.With(Ui.H1("Library"), t => t.Margin = new Thickness(0)));
        var content = new StackPanel { Children = { header } };
        if (Data.Broken.Count > 0)
            content.Children.Add(Ui.With(Ui.Notice($"{Data.Broken.Count} lesson file{(Data.Broken.Count == 1 ? " has" : "s have")} errors and {(Data.Broken.Count == 1 ? "is" : "are")} not shown: {string.Join(", ", Data.Broken.Select(b => b.File))}. Fix or re-import {(Data.Broken.Count == 1 ? "it" : "them")}."), n => n.Margin = new Thickness(0, 0, 0, 16)));
        content.Children.Add(filters);
        content.Children.Add(list);
        root = Ui.Page(content);
        var folder = Data.Store.Root is { } r ? Path.Combine(r, "lessons") : null;
        Menus.Attach(root, _ =>
        [
            new MenuEntry("Import a lesson", () => Go("#/import"), Icons.Import),
            new MenuEntry("Ask Claude for a lesson", () => Go("#/ask"), Icons.Ask),
            null,
            new MenuEntry("Refresh library", () => RefreshAsync().Forget(), Icons.Refresh, "F5"),
            new MenuEntry("Open lessons folder", folder != null ? () => ShowInExplorer(folder) : null, Icons.Folder),
            null,
            new MenuEntry("Clear filters", trackFilter != "all" || statusFilter != "all" ? () => { trackFilter = statusFilter = "all"; Draw(); } : null, Icons.Clear),
        ], outermost: true);
        Draw();
    }

    private static async Task RefreshAsync()
    {
        await Data.ReloadLessonsAsync();
        Reload();
    }

    private static string StatusOf(LessonDoc l) => Data.Progress.Lessons.GetValueOrDefault(l.Meta.Id)?.Status ?? "new";

    private void Draw()
    {
        var all = Data.Lessons.Values.ToList();
        var tracks = Data.Curriculum.Tracks.Where(t => all.Any(l => l.Meta.Track == t.Code)).ToList();
        filters.Children.Clear();
        filters.Children.Add(ChipRow("Track", [("all", "All"), .. tracks.Select(t => (t.Code, t.Name))], trackFilter, v => trackFilter = v));
        filters.Children.Add(ChipRow("Status", [("all", "All"), .. StatusLabel.Select(kv => (kv.Key, kv.Value))], statusFilter, v => statusFilter = v));

        var shown = all.Where(l => (trackFilter == "all" || l.Meta.Track == trackFilter) && (statusFilter == "all" || StatusOf(l) == statusFilter)).ToList();
        list.Children.Clear();
        foreach (var t in Data.Curriculum.Tracks)
        {
            var lessons = shown.Where(l => l.Meta.Track == t.Code).OrderBy(l => l.Meta.Tier).ThenBy(l => l.Meta.Title).ToList();
            if (lessons.Count == 0) continue;
            var h = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 8) };
            h.Children.Add(Ui.With(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 8, 0) }, b => b.Res(Border.BackgroundProperty, $"Track{t.Code}")));
            h.Children.Add(Ui.Text(t.Name, "FontH3", "Text", FontWeights.SemiBold));
            h.Children.Add(Ui.With(Ui.Muted($"  ({lessons.Count})", "FontSmall"), x => x.VerticalAlignment = VerticalAlignment.Center));
            list.Children.Add(h);
            foreach (var l in lessons) list.Children.Add(Item(l));
        }
        if (list.Children.Count == 0) list.Children.Add(Ui.Muted(all.Count > 0 ? "No lessons match these filters." : "Your library is empty."));
    }

    private FrameworkElement ChipRow(string label, (string Value, string Text)[] options, string current, Action<string> set)
    {
        var row = new WrapPanel();
        var group = Guid.NewGuid().ToString("N");
        row.Children.Add(Ui.With(Ui.Muted(label, "FontSmall"), t => { t.Width = 60; t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 0, 6); }));
        foreach (var (value, text) in options)
        {
            var b = new RadioButton { Content = text, IsChecked = current == value, GroupName = group };
            b.Res(FrameworkElement.StyleProperty, "SegItem");
            var border = new Border { Child = b, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true, Margin = new Thickness(0, 0, 6, 6) };
            border.Res(Border.BorderBrushProperty, "Border");
            b.Checked += (_, _) => { set(value); Draw(); };
            System.Windows.Automation.AutomationProperties.SetName(b, $"{label}: {text}");
            row.Children.Add(border);
        }
        return row;
    }

    private FrameworkElement Item(LessonDoc l)
    {
        var id = l.Meta.Id;
        var meta = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var c in new[] { StatusLabel[StatusOf(l)], $"Tier {l.Meta.Tier}", $"Energy {l.Meta.Energy}", $"~{l.Meta.Minutes} min", Data.Curriculum.UnitById(l.Meta.Unit)?.Title ?? l.Meta.Unit })
            meta.Children.Add(Ui.Chip(c));
        var body = new StackPanel { Children = { Ui.Text(l.Meta.Title, "FontBase", "Text", FontWeights.SemiBold), Ui.Muted(l.Meta.Summary, "FontSmall"), meta } };
        var b = new Button { Content = body, Tag = l, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(16, 12, 16, 8) };
        b.Res(FrameworkElement.StyleProperty, "Btn");
        b.Res(Control.BackgroundProperty, "Surface");
        System.Windows.Automation.AutomationProperties.SetName(b, $"{l.Meta.Title}. {l.Meta.Summary}");
        b.Click += (_, _) => Go(Data.LessonRoute(id));
        Menus.Attach(b, _ => LessonMenu(l));
        return b;
    }

    /// <summary>Everything you can do with a lesson in the library.</summary>
    private IEnumerable<MenuEntry?> LessonMenu(LessonDoc l)
    {
        var id = l.Meta.Id;
        var lp = Data.Progress.Lessons.GetValueOrDefault(id);
        var file = LessonFile(id);
        var hasProgress = lp is { Status: not "new" } || lp?.Attempts.Count > 0 || Data.Progress.Questions.Keys.Any(k => k.StartsWith($"{id}#"));
        return
        [
            new MenuEntry("Open", () => Go($"#/lesson/{id}"), Icons.Page, "Enter"),
            new MenuEntry($"Practise ({l.Questions.Count} questions)", l.Questions.Count > 0 ? () => Go($"#/lesson/{id}/practice") : null, Icons.Play),
            lp?.Run is { Finished: false, Results.Count: > 0 } ? new MenuEntry("Continue practice where you left off", () => Go($"#/lesson/{id}/practice"), Icons.Review) : null,
            null,
            new MenuEntry("Copy title", () => Menus.Copy(l.Meta.Title), Icons.Copy),
            new MenuEntry("Copy lesson id", () => Menus.Copy(id), Icons.Copy),
            new MenuEntry("Ask Claude for a follow-up lesson", () => Go($"#/ask?request={Uri.EscapeDataString($"A follow-up to the lesson \"{l.Meta.Title}\" ({id}).")}"), Icons.Ask),
            null,
            new MenuEntry("Duplicate", () => DuplicateAsync(l).Forget(), Icons.Duplicate),
            new MenuEntry("Open file in editor", file != null ? () => OpenInEditor(file) : null, Icons.Edit),
            new MenuEntry("Show file in Explorer", file != null ? () => ShowInExplorer(file) : null, Icons.Folder),
            null,
            new MenuEntry("Reset progress…", hasProgress ? () => ResetProgress(l) : null, Icons.Reset),
            new MenuEntry("Delete lesson…", file != null ? () => DeleteAsync(l, file).Forget() : null, Icons.Delete, "Del"),
        ];
    }

    private async Task DuplicateAsync(LessonDoc l)
    {
        var file = LessonFile(l.Meta.Id);
        var text = file != null && File.Exists(file) ? Parser.NormalizeText(await File.ReadAllTextAsync(file)) : null;
        if (text == null) return;
        var newId = Parser.NextFreeId(l.Meta.Id, Data.Lessons.ContainsKey);
        await Data.SaveLessonFileAsync(newId, Parser.ReplaceLessonId(text, newId));
        Data.Progress.Lesson(newId).Imported = DateTime.UtcNow.ToString("O");
        await Data.SaveProgressAsync();
        Draw();
    }

    private void ResetProgress(LessonDoc l)
    {
        var ok = MessageBox.Show(Application.Current.MainWindow,
            $"Reset your progress on \"{l.Meta.Title}\"?\n\nIts status, scores and review schedule are cleared. The lesson itself stays.",
            "Reset progress", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (ok != MessageBoxResult.Yes) return;
        var id = l.Meta.Id;
        var imported = Data.Progress.Lessons.GetValueOrDefault(id)?.Imported;
        Data.Progress.Lessons[id] = new LessonProgress { Imported = imported };
        ProgressStore.PruneLessonQuestions(Data.Progress, id, []);
        Data.SaveProgressAsync().Forget();
        Draw();
    }

    private async Task DeleteAsync(LessonDoc l, string file)
    {
        var ok = MessageBox.Show(Application.Current.MainWindow,
            $"Delete \"{l.Meta.Title}\"?\n\nThe file goes to the Recycle Bin, so you can restore it. Your progress on it is kept in case you re-import it.",
            "Delete lesson", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (ok != MessageBoxResult.Yes) return;
        RecycleBin(file);
        await Data.ReloadLessonsAsync();
        Draw();
    }

    public override bool OnKey(KeyEventArgs e)
    {
        if (e.Key == Key.F5) { RefreshAsync().Forget(); return true; }
        if (e.Key == Key.Delete && Keyboard.FocusedElement is Button { Tag: LessonDoc l } && LessonFile(l.Meta.Id) is { } f) { DeleteAsync(l, f).Forget(); return true; }
        return false;
    }

    // Recycle Bin via the shell, so a deleted lesson can be restored.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT { public nint hwnd; public uint wFunc; public string pFrom; public string? pTo; public ushort fFlags; public int fAnyOperationsAborted; public nint hNameMappings; public string? lpszProgressTitle; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    private static void RecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT { wFunc = 3 /* FO_DELETE */, pFrom = path + "\0\0", fFlags = 0x0040 | 0x0010 | 0x0004 /* ALLOWUNDO | NOCONFIRMATION | SILENT */ };
        SHFileOperation(ref op);
    }
}
