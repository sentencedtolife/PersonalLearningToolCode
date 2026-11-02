// Map: tracks as rows, units as cells coloured by mastery. Hover (or focus) shows the rules.
using System.Windows;
using System.Windows.Controls;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using Nordlys.Core;
using Nordlys.Core.Progress;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed class MapView : View
{
    private readonly ScrollViewer root;
    private readonly ContentControl detail = new() { Margin = new Thickness(0, 16, 0, 0) };
    private readonly Dictionary<string, int> levels = Data.Levels();
    private readonly List<Button> cells = [];

    public override string Title => "Map";
    public override FrameworkElement Root => root;

    private static string Tip(Unit u, int level) =>
        string.Join('\n', new[] { $"{u.Title} ({u.Id})", $"Now: {Mastery.LevelRules[level]}", level < 3 ? $"Next: {Mastery.LevelRules[level + 1]}" : null, u.Prereqs.Length > 0 ? $"Builds on: {string.Join(", ", u.Prereqs)}" : null }.OfType<string>());

    public MapView(string? selectUnit)
    {
        var legend = new WrapPanel { Margin = new Thickness(0, 0, 0, 20) };
        for (var i = 0; i < 4; i++)
        {
            var sw = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 6, 0) };
            sw.Res(Border.BackgroundProperty, $"Mastery{i}");
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0), ToolTip = Mastery.LevelRules[i], Focusable = true, Background = System.Windows.Media.Brushes.Transparent };
            item.Children.Add(sw);
            item.Children.Add(Ui.Text(Mastery.LevelNames[i], "FontSmall"));
            legend.Children.Add(item);
        }

        var rows = new StackPanel();
        foreach (var t in Data.Curriculum.Tracks)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var name = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 8, 0) };
            name.Children.Add(Ui.With(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 8, 0) }, b => b.Res(Border.BackgroundProperty, $"Track{t.Code}")));
            name.Children.Add(Ui.Text(t.Name, "FontSmall", "Text", FontWeights.SemiBold));
            var wrap = new WrapPanel();
            foreach (var u in t.Units)
            {
                var level = levels.GetValueOrDefault(u.Id);
                var text = Ui.Text(u.Title, "FontSmall", level == 0 ? "Text" : "Bg");
                text.TextWrapping = TextWrapping.NoWrap;
                var cell = new Button { Content = text, ToolTip = Tip(u, level), Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 5, 10, 5), MinHeight = 0, Tag = u.Id };
                cell.Res(FrameworkElement.StyleProperty, "Btn").Res(Control.BackgroundProperty, $"Mastery{level}").Res(Control.BorderBrushProperty, $"Mastery{level}");
                ToolTipService.SetShowOnDisabled(cell, true);
                System.Windows.Automation.AutomationProperties.SetName(cell, $"{u.Title}: {Mastery.LevelNames[level]}");
                var unit = u;
                cell.Click += (_, _) => Select(unit, cell);
                Menus.Attach(cell, _ => UnitMenu(unit));
                cells.Add(cell);
                wrap.Children.Add(cell);
            }
            Grid.SetColumn(wrap, 1);
            g.Children.Add(name);
            g.Children.Add(wrap);
            rows.Children.Add(g);
        }

        root = Ui.Page(new StackPanel { Children = { Ui.H1("Map"), Ui.With(Ui.Muted("Each cell is a unit. Its colour is your mastery level. Hover or focus a cell to see the rules; click it to see its lessons."), t => t.Margin = new Thickness(0, 0, 0, 16)), legend, rows, detail } }, 1000);
        Menus.Attach(root, _ =>
        [
            new MenuEntry("Ask Claude for the next lesson", () => Go("#/ask"), Icons.Ask),
            new MenuEntry("Open Library", () => Go("#/library"), Icons.Library),
        ], outermost: true);

        if (selectUnit != null && Data.Curriculum.UnitById(selectUnit) is { } su) Select(su, cells.First(c => (string)c.Tag == su.Id));
    }

    private IEnumerable<MenuEntry?> UnitMenu(Unit u)
    {
        var lessons = Data.Lessons.Values.Where(l => l.Meta.Unit == u.Id).OrderBy(l => l.Meta.Tier).ToList();
        var list = new List<MenuEntry?>();
        foreach (var l in lessons.Take(5)) list.Add(new MenuEntry($"Open \"{l.Meta.Title}\"", () => Go(Data.LessonRoute(l.Meta.Id)), Icons.Page));
        if (lessons.Count == 0) list.Add(new MenuEntry("No lessons for this unit yet", null, Icons.Info));
        list.Add(null);
        list.Add(new MenuEntry("Ask Claude for a lesson on this unit", () => Go($"#/ask?unit={Uri.EscapeDataString(u.Id)}"), Icons.Ask));
        list.Add(new MenuEntry("Copy unit id", () => Menus.Copy(u.Id), Icons.Copy));
        list.Add(new MenuEntry("Copy mastery rules", () => Menus.Copy(Tip(u, levels.GetValueOrDefault(u.Id))), Icons.Copy));
        return list;
    }

    private void Select(Unit u, Button cell)
    {
        foreach (var c in cells) c.BorderThickness = new Thickness(c == cell ? 2 : 1);
        foreach (var c in cells) c.Res(Control.BorderBrushProperty, c == cell ? "Text" : $"Mastery{levels.GetValueOrDefault((string)c.Tag)}");
        var lessons = Data.Lessons.Values.Where(l => l.Meta.Unit == u.Id).OrderBy(l => l.Meta.Tier).ThenBy(l => l.Meta.Id).ToList();
        var level = levels.GetValueOrDefault(u.Id);
        var stack = Ui.Stack(Ui.With(Ui.H2(u.Title), t => t.Margin = new Thickness(0)), Ui.Muted($"{u.Id} · {Mastery.LevelNames[level]} · tier {string.Join("–", u.Tiers)}", "FontSmall"));
        if (lessons.Count == 0) stack.Children.Add(Ui.With(Ui.Muted("No lessons for this unit in your library yet."), t => t.Margin = new Thickness(0, 12, 0, 0)));
        foreach (var l in lessons)
        {
            var status = Data.Progress.Lessons.GetValueOrDefault(l.Meta.Id)?.Status ?? "new";
            var b = Ui.Btn($"{l.Meta.Title} ({(status == "practiced" ? "practised" : status == "read" ? "in progress" : "new")})", () => Go(Data.LessonRoute(l.Meta.Id)));
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(b);
        }
        var ask = Ui.Btn("Ask Claude for a lesson on this unit", () => Go($"#/ask?unit={Uri.EscapeDataString(u.Id)}"), lessons.Count > 0 ? "BtnFlat" : "BtnPrimary");
        ask.HorizontalAlignment = HorizontalAlignment.Left;
        ask.Margin = new Thickness(0, 12, 0, 0);
        stack.Children.Add(ask);
        detail.Content = Ui.Card(stack);
    }
}
