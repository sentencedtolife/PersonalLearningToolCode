// Import: paste (or drop, or open a file), validate, save. Errors block; warnings don't.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed class ImportView : View
{
    private readonly ScrollViewer root;
    private readonly TextBox paste = new() { AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.NoWrap, MinHeight = 280, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Tag = "custom-menu" };
    private readonly ContentControl report = new() { Margin = new Thickness(0, 8, 0, 0) };

    public override string Title => "Import";
    public override FrameworkElement Root => root;

    public ImportView()
    {
        paste.Res(Control.FontFamilyProperty, "MonoFont");
        paste.FontSize = 13;
        System.Windows.Automation.AutomationProperties.SetName(paste, "Paste the lesson from Claude here, or drop a .lesson.md file.");
        var hint = Ui.Muted("Paste the lesson from Claude here (chat text around it is fine), or drop a .lesson.md file anywhere on this screen.", "FontSmall");

        Menus.Attach(paste, _ => Menus.ForTextBox(paste).Concat(
        [
            null,
            new MenuEntry("Paste and import", Clipboard.ContainsText() ? () => { paste.Text = Clipboard.GetText(); RunImportAsync().Forget(); } : null, Icons.Paste),
            new MenuEntry("Import lesson", paste.Text.Trim() != "" ? () => RunImportAsync().Forget() : null, Icons.Import, "Ctrl+Enter"),
            new MenuEntry("Open a .lesson.md file…", () => OpenFileAsync().Forget(), Icons.Open, "Ctrl+O"),
            new MenuEntry("Clear", paste.Text != "" ? () => { paste.Clear(); report.Content = null; } : null, Icons.Clear),
        ]));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ui.Btn("Import lesson", () => RunImportAsync().Forget(), "BtnPrimary", "Ctrl+Enter"), Ui.With(Ui.Btn("Open a .lesson.md file…", () => OpenFileAsync().Forget(), "BtnFlat", "Ctrl+O"), b => b.Margin = new Thickness(8, 0, 0, 0)) } };
        root = Ui.Page(Ui.Stack(Ui.H1("Import a lesson"), hint, paste, buttons, report));
        root.AllowDrop = true;
        root.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        root.Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) LoadFileAsync(files[0]).Forget(); };
        paste.PreviewDragOver += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; } };
        paste.PreviewDrop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) { e.Handled = true; LoadFileAsync(files[0]).Forget(); } };
        Menus.Attach(root, _ =>
        [
            new MenuEntry("Paste and import", Clipboard.ContainsText() ? () => { paste.Text = Clipboard.GetText(); RunImportAsync().Forget(); } : null, Icons.Paste),
            new MenuEntry("Open a .lesson.md file…", () => OpenFileAsync().Forget(), Icons.Open, "Ctrl+O"),
            new MenuEntry("Ask Claude for a lesson", () => Go("#/ask"), Icons.Ask),
        ], outermost: true);
    }

    private async Task OpenFileAsync()
    {
        // The standard Windows Open dialog.
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Import a lesson", Filter = "Lesson files (*.lesson.md;*.md)|*.lesson.md;*.md|All files (*.*)|*.*" };
        if (dlg.ShowDialog(Application.Current.MainWindow) == true) await LoadFileAsync(dlg.FileName);
    }

    private async Task LoadFileAsync(string path)
    {
        paste.Text = await File.ReadAllTextAsync(path);
        await RunImportAsync();
    }

    private static FrameworkElement IssueList(string title, string brush, IReadOnlyList<Issue> issues)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 16, 0, 0), Children = { Ui.Text($"{title} ({issues.Count})", "FontBase", brush, FontWeights.SemiBold) } };
        foreach (var i in issues)
        {
            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            line.Res(TextBlock.FontSizeProperty, "FontSmall").Res(TextBlock.ForegroundProperty, "Text");
            line.Inlines.Add(new System.Windows.Documents.Run($"{Validator.Where(i)}, {i.Field}: ").ResC(System.Windows.Documents.TextElement.ForegroundProperty, brush));
            line.Inlines.Add(new System.Windows.Documents.Run(i.Message));
            if (i.Fix != "") { line.Inlines.Add(new System.Windows.Documents.LineBreak()); line.Inlines.Add(new System.Windows.Documents.Run($"Fix: {i.Fix}").ResC(System.Windows.Documents.TextElement.ForegroundProperty, "TextMuted")); }
            var holder = new Border { Child = line, Background = System.Windows.Media.Brushes.Transparent };
            var issue = i;
            Menus.Attach(holder, _ =>
            [
                new MenuEntry("Copy this line", () => Menus.Copy(Validator.FormatIssue(issue)), Icons.Copy),
                new MenuEntry("Copy error report", issues.Any(x => x.Severity == Severity.Error) ? () => Menus.Copy(Validator.ErrorReport(issues.Where(x => x.Severity == Severity.Error))) : null, Icons.Copy),
            ]);
            stack.Children.Add(holder);
        }
        return stack;
    }

    private async Task RunImportAsync()
    {
        var input = paste.Text;
        if (input.Trim() == "") { report.Content = Ui.Muted("Paste a lesson first."); return; }
        var r = Validator.Load(input, Data.KnownUnits());
        if (r.Lesson is null)
        {
            var copyBtn = Ui.Btn("Copy error report", () => { }, "BtnPrimary");
            copyBtn.HorizontalAlignment = HorizontalAlignment.Left;
            copyBtn.Click += (_, _) => { Menus.Copy(Validator.ErrorReport(r.Errors)); ((TextBlock)((StackPanel)copyBtn.Content).Children[0]).Text = "Copied. Paste it back to Claude."; };
            report.Content = Ui.Stack(
                Ui.Text("The lesson was not imported. Copy the error report and paste it back to Claude in the same chat; it will send a corrected lesson."),
                copyBtn,
                IssueList("Errors", "Error", r.Errors),
                r.Warnings.Count > 0 ? IssueList("Warnings", "NotYet", r.Warnings) : null);
            return;
        }
        var id = r.Lesson.Meta.Id;
        if (!Data.Lessons.TryGetValue(id, out var existing)) { await FinishAsync(id, r.Text, r.Warnings, replaced: false); return; }

        var newId = Parser.NextFreeId(id, Data.Lessons.ContainsKey);
        var replace = Ui.Btn("Replace", () => FinishAsync(id, r.Text, r.Warnings, replaced: true).Forget(), "BtnPrimary");
        var both = Ui.Btn("Keep both", () =>
        {
            var text = Parser.ReplaceLessonId(r.Text, newId);
            _ = FinishAsync(newId, text, Validator.Load(text, Data.KnownUnits()).Warnings, replaced: false);
        });
        both.Margin = new Thickness(8, 0, 0, 0);
        report.Content = Ui.Stack(
            Ui.Text("Replace existing lesson?", "FontBase", "NotYet", FontWeights.Bold),
            Ui.Text($"You already have a lesson with the id \"{id}\": {existing.Meta.Title}. Replace keeps your progress for questions that still exist. Keep both saves the new one as \"{newId}\"."),
            new StackPanel { Orientation = Orientation.Horizontal, Children = { replace, both } });
        replace.Focus();
    }

    private async Task FinishAsync(string id, string text, IReadOnlyList<Issue> warnings, bool replaced)
    {
        if (replaced && Validator.Load(text, Data.KnownUnits()).Lesson is { } lesson)
            ProgressStore.PruneLessonQuestions(Data.Progress, id, lesson.Questions.Select(q => q.Id));
        await Data.SaveLessonFileAsync(id, text);
        if (!replaced) Data.Progress.Lesson(id).Imported = DateTime.UtcNow.ToString("O");
        await Data.SaveProgressAsync();
        paste.Clear();
        var title = Data.Lessons[id].Meta.Title;
        var open = Ui.Btn("Open lesson", () => Go($"#/lesson/{id}"), "BtnPrimary", "Enter");
        open.HorizontalAlignment = HorizontalAlignment.Left;
        report.Content = Ui.Stack(
            Ui.Text($"Imported: {title}", "FontBase", "Correct", FontWeights.Bold),
            Ui.Text($"Saved as {id}.lesson.md{(replaced ? ", replacing the old version. Progress was kept for questions that still exist" : "")}."),
            warnings.Count > 0 ? IssueList("Warnings (the lesson was still imported)", "NotYet", warnings) : null,
            open);
        open.Focus();
    }

    public override void Shown() => paste.Focus();

    public override bool OnKey(KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (e.Key == Key.Enter && ctrl) { RunImportAsync().Forget(); return true; }
        if (e.Key == Key.O && ctrl) { OpenFileAsync().Forget(); return true; }
        return false;
    }
}
