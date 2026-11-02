// Settings: appearance, motion, window, data.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Nordlys.App.Shell;
using Nordlys.App.Theme;
using Nordlys.App.UI;
using Nordlys.Core;
using Nordlys.Core.Progress;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed class SettingsView : View
{
    private readonly StackPanel stack = new();
    private readonly ScrollViewer root;
    private readonly TextBlock status = Ui.Text("", "FontSmall");
    private bool confirmingReset;

    public override string Title => "Settings";
    public override FrameworkElement Root => root;

    public SettingsView()
    {
        root = Ui.Page(stack, 680);
        Menus.Attach(root, _ =>
        [
            new MenuEntry("Open data folder", Data.Store.Root != null ? OpenDataFolder : null, Icons.Folder),
            new MenuEntry("Export progress backup…", ExportBackup, Icons.Copy),
        ], outermost: true);
        Draw();
    }

    private void Save(Action<Settings> change)
    {
        change(Data.Settings);
        ThemeManager.Apply(Data.Settings);
        Data.SaveSettingsAsync().Forget();
        Draw();
    }

    private void Say(string text, bool ok = true)
    {
        status.Text = text;
        status.Res(TextBlock.ForegroundProperty, ok ? "Correct" : "NotYet");
    }

    private FrameworkElement Row(string label, string? help, UIElement control)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Children = { Ui.Text(label, "FontBase", "Text", FontWeights.SemiBold) }, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        if (help != null) text.Children.Add(Ui.Muted(help, "FontSmall"));
        Grid.SetColumn((FrameworkElement)control, 1);
        ((FrameworkElement)control).VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(text);
        g.Children.Add(control);
        var b = new Border { Child = g, BorderThickness = new Thickness(0, 0, 0, 1) };
        return b.Res(Border.BorderBrushProperty, "Border").Res(Border.PaddingProperty, "PadSetting");
    }

    private Border Choice<T>(Func<Settings, T> get, Action<Settings, T> set, params (T Value, string Label)[] options) =>
        Ui.Segmented(options.Select(o => (o.Label, EqualityComparer<T>.Default.Equals(get(Data.Settings), o.Value), (Action)(() => Save(s => set(s, o.Value))))).ToArray());

    private void Draw()
    {
        stack.Children.Clear();
        stack.Children.Add(Ui.H1("Settings"));

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        var names = new Dictionary<string, string> { ["frost"] = "Frost", ["teal"] = "Teal", ["blue"] = "Blue", ["deep"] = "Deep blue", ["green"] = "Green", ["purple"] = "Purple", ["orange"] = "Orange" };
        foreach (var a in Settings.Accents)
        {
            var on = Data.Settings.Accent == a;
            var b = new Button { Width = 26, Height = 26, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 0), ToolTip = names[a], BorderThickness = new Thickness(on ? 2 : 0), Background = new System.Windows.Media.SolidColorBrush(Nord.Accents[a]) };
            b.Res(FrameworkElement.StyleProperty, "Btn");
            b.Background = new System.Windows.Media.SolidColorBrush(Nord.Accents[a]);
            b.Res(Control.BorderBrushProperty, "Text");
            System.Windows.Automation.AutomationProperties.SetName(b, $"Accent colour: {names[a]}{(on ? " (selected)" : "")}");
            var accent = a;
            b.Click += (_, _) => Save(s => s.Accent = accent);
            swatches.Children.Add(b);
        }

        var motion = new CheckBox { Content = "No animations", IsChecked = Data.Settings.ReduceMotion };
        motion.Click += (_, _) => Save(s => s.ReduceMotion = motion.IsChecked == true);

        UIElement reset = confirmingReset
            ? new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    Ui.Btn("Yes, reset it", () => Data.ResetGeneratorPromptAsync().ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => { confirmingReset = false; Draw(); Say("The generator prompt is back to the default."); })).Forget(), "BtnPrimary"),
                    Ui.Btn("Cancel", () => { confirmingReset = false; Draw(); }, "BtnFlat"),
                },
            }
            : Ui.Btn("Reset generator prompt to default", () => { confirmingReset = true; Draw(); });

        foreach (var row in new[]
        {
            Row("Theme", null, Choice(s => s.Theme, (s, v) => s.Theme = v, ("dark", "Dark"), ("light", "Light"))),
            Row("Accent colour", "Buttons, links and highlights.", swatches),
            Row("Density", "Compact fits more on the screen.", Choice(s => s.Density, (s, v) => s.Density = v, ("comfortable", "Comfortable"), ("compact", "Compact"))),
            Row("Paper font", "The font used for lesson text.", Choice(s => s.PaperFont, (s, v) => s.PaperFont = v, ("sans", "Sans (Segoe UI)"), ("serif", "Serif (Cambria)"))),
            Row("Font size", null, Choice(s => s.FontSize, (s, v) => s.FontSize = v, ("S", "S"), ("M", "M"), ("L", "L"))),
            Row("Reduce motion", "Turns off all animations. They are also off when Windows' \"Animation effects\" setting is off.", motion),
            Row("Resize preview", "How see-through the blue rectangle is when you drag the corner grip (or press Alt+F8).", Choice(s => s.MarqueeAlpha, (s, v) => s.MarqueeAlpha = v, (0.15, "Light"), (0.25, "Medium"), (0.4, "Strong"))),
            Row("Data folder", "Lessons, progress, backups, curriculum.json and generator-prompt.md.", Ui.Btn("Open data folder", OpenDataFolder)),
            Row("Generator prompt", "Replaces your copy of generator-prompt.md with the one that came with the app.", reset),
            Row("Backup", "Saves a copy of progress.json wherever you choose.", Ui.Btn("Export progress backup…", ExportBackup)),
        }) stack.Children.Add(row);

        stack.Children.Add(Ui.With(status, s => s.Margin = new Thickness(0, 12, 0, 0)));
        stack.Children.Add(Ui.With(Ui.Text($"Nordlys {AppState.AppVersion} · started in {App.StartupMs:0} ms · data: {Data.Store.Root}", "FontTiny", "TextFaint"), t => t.Margin = new Thickness(0, 24, 0, 0)));
    }

    private void OpenDataFolder()
    {
        if (Data.Store.Root is { } r) Process.Start(new ProcessStartInfo(r) { UseShellExecute = true });
    }

    private void ExportBackup()
    {
        // The standard Windows Save dialog.
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Export progress backup", FileName = $"nordlys-progress-{Dates.Today()}.json", Filter = "JSON (*.json)|*.json" };
        if (dlg.ShowDialog(Application.Current.MainWindow) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, ProgressStore.Serialize(Data.Progress));
            Say($"Saved: {dlg.FileName}");
        }
        catch (IOException e)
        {
            Say($"Could not save: {e.Message}", ok: false);
        }
    }
}
