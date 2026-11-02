// Home: one big "Next" button. Next asks for energy, then picks a lesson.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using Nordlys.Core.Progress;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed class HomeView : View
{
    private enum Step { Start, Energy, None }
    private Step step = Step.Start;
    private int chosen = Data.Settings.LastEnergy;
    private readonly StackPanel panel = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid root = new() { Background = System.Windows.Media.Brushes.Transparent };

    public override string Title => "Nordlys";
    public override FrameworkElement Root => root;

    /// <summary>A quiet line at the bottom when an update is available: never competes with Next.</summary>
    private readonly StackPanel updateBar = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 20) };

    public HomeView()
    {
        root.Children.Add(panel);
        root.Children.Add(updateBar);
        UpdateService.Changed += DrawUpdate;
        DrawUpdate();
        Menus.Attach(root, _ =>
        [
            new MenuEntry("Next lesson", () => { step = Step.Energy; Draw(); }, Icons.Play, "Enter"),
            InProgress() is { } l ? new MenuEntry($"Continue: {l.Meta.Title}", () => Go(Data.LessonRoute(l.Meta.Id)), Icons.Open) : null,
            Data.DueCount() > 0 ? new MenuEntry($"Review ({Data.DueCount()} due)", () => Go("#/review"), Icons.Review) : null,
            null,
            new MenuEntry("Ask Claude for a lesson", () => Go("#/ask"), Icons.Ask),
            new MenuEntry("Import a lesson", () => Go("#/import"), Icons.Import),
        ], outermost: true);
        Draw();
    }

    public override void Closed() => UpdateService.Changed -= DrawUpdate;

    private void DrawUpdate()
    {
        updateBar.Children.Clear();
        var v = UpdateService.Release?.Version.ToString(3);
        void Line(string text, Button? action = null)
        {
            updateBar.Children.Add(Ui.With(Ui.Muted(text, "FontSmall"), t => t.VerticalAlignment = VerticalAlignment.Center));
            if (action != null) { action.Margin = new Thickness(8, 0, 0, 0); action.MinHeight = 0; action.Padding = new Thickness(10, 3, 10, 3); updateBar.Children.Add(action); }
        }
        switch (UpdateService.State)
        {
            case UpdateService.Phase.Available: Line($"Nordlys {v} is available.", Ui.Btn("Update", () => UpdateService.DownloadAsync().Forget(), "BtnFlat")); break;
            case UpdateService.Phase.Downloading: Line($"Downloading Nordlys {v}… {UpdateService.Progress:P0}"); break;
            case UpdateService.Phase.Ready: Line($"Nordlys {v} is ready.", Ui.Btn("Restart to update", UpdateService.RestartToUpdate, "BtnFlat")); break;
        }
    }

    private Nordlys.Core.Lesson.LessonDoc? InProgress() =>
        NextLesson.Pick(Data.Lessons, Data.Progress, 3, Data.Levels()) is NextPick.Resume r ? Data.Lessons.GetValueOrDefault(r.LessonId) : null;

    private void Pick(int energy)
    {
        Data.Energy = energy;
        chosen = energy;
        Data.Settings.LastEnergy = energy;
        Data.SaveSettingsAsync().Forget();
        switch (NextLesson.Pick(Data.Lessons, Data.Progress, energy, Data.Levels()))
        {
            case NextPick.Resume r: Go(Data.LessonRoute(r.LessonId)); break;
            case NextPick.New n: Go(Data.LessonRoute(n.LessonId)); break;
            default: step = Step.None; Draw(); break;
        }
    }

    private void Draw()
    {
        panel.Children.Clear();
        void Add(FrameworkElement e, double top = 0) { e.HorizontalAlignment = HorizontalAlignment.Center; e.Margin = new Thickness(0, top, 0, 0); panel.Children.Add(e); }
        switch (step)
        {
            case Step.Start:
            {
                if (Data.Notice is { } notice) { Add(Ui.Notice(notice)); Data.Notice = null; }
                var next = Ui.Btn("Next", () => { step = Step.Energy; Draw(); }, "BtnPrimary", big: true);
                Add(next, panel.Children.Count > 0 ? 24 : 0);
                if (InProgress() is { } l) Add(Ui.Btn($"Continue: {l.Meta.Title}", () => Go(Data.LessonRoute(l.Meta.Id)), "BtnFlat"), 24);
                var due = Data.DueCount();
                if (due > 0) Add(Ui.Btn($"Review ({due} due)", () => Go("#/review"), "BtnFlat"), 8);
                next.Loaded += (_, _) => next.Focus();
                break;
            }
            case Step.Energy:
            {
                Add(Ui.H1("How much energy do you have right now?"));
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var (n, desc) in new[] { (1, "Low · 5–10 min"), (2, "Medium · ~25 min"), (3, "High · 45–90 min") })
                {
                    var b = EnergyButton(n, desc, n == chosen, () => Pick(n));
                    b.Margin = new Thickness(6);
                    row.Children.Add(b);
                    if (n == chosen) b.Loaded += (_, _) => b.Focus();
                }
                Add(row, 8);
                Add(Ui.Muted("Press 1, 2 or 3. Esc goes back.", "FontSmall"), 12);
                break;
            }
            default:
                Add(Ui.H1("No lesson ready"));
                Add(Ui.Muted($"There is no unpractised lesson for energy {chosen} or lower in your library."));
                var ask = Ui.Btn("No lesson ready. Ask Claude for one →", () => Go($"#/ask?energy={chosen}"), "BtnPrimary", "Enter");
                Add(ask, 20);
                ask.Loaded += (_, _) => ask.Focus();
                break;
        }
    }

    public static Button EnergyButton(int n, string desc, bool active, Action onPick)
    {
        var num = Ui.Text(n.ToString(), "FontBig", "Accent", FontWeights.Bold);
        num.HorizontalAlignment = HorizontalAlignment.Center;
        var d = Ui.Muted(desc, "FontSmall");
        d.HorizontalAlignment = HorizontalAlignment.Center;
        var b = new Button { Content = new StackPanel { Children = { num, d } }, Width = 150, Padding = new Thickness(12, 14, 12, 14), BorderThickness = new Thickness(active ? 2 : 1) };
        b.Res(FrameworkElement.StyleProperty, "Btn").Res(Control.BackgroundProperty, "Surface").Res(Control.BorderBrushProperty, active ? "Accent" : "Border");
        System.Windows.Automation.AutomationProperties.SetName(b, $"Energy {n}: {desc}");
        b.Click += (_, _) => onPick();
        return b;
    }

    public override bool OnKey(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return false;
        if (step != Step.None && e.Key is Key.D1 or Key.D2 or Key.D3 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3)
        {
            Pick(e.Key is Key.D1 or Key.NumPad1 ? 1 : e.Key is Key.D2 or Key.NumPad2 ? 2 : 3);
            return true;
        }
        if (e.Key == Key.Escape && step != Step.Start) { step = Step.Start; Draw(); return true; }
        return false;
    }
}
