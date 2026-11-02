// Glossary: every term from lessons you have opened, A–Z, with where it first appeared.
using System.Windows;
using System.Windows.Controls;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed class GlossaryView : View
{
    private readonly ScrollViewer root;
    private readonly FrameworkElement? highlighted;

    public override string Title => "Glossary";
    public override FrameworkElement Root => root;

    public GlossaryView(string? focusTerm)
    {
        var entries = Data.Progress.Glossary.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var stack = new StackPanel { Children = { Ui.H1("Glossary"), Ui.Muted(entries.Count > 0 ? $"{entries.Count} term{(entries.Count == 1 ? "" : "s")} from the lessons you have opened." : "Terms appear here when you open a lesson that defines them.") } };
        foreach (var group in entries.GroupBy(kv => char.IsLetter(kv.Key[0]) ? char.ToUpperInvariant(kv.Key[0]) : '#'))
        {
            var letter = Ui.Text(group.Key.ToString(), "FontH3", "Accent", FontWeights.SemiBold);
            stack.Children.Add(new Border { Child = letter, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 24, 0, 8), Padding = new Thickness(0, 0, 0, 4) }.Res(Border.BorderBrushProperty, "Border"));
            foreach (var (term, g) in group)
            {
                var lesson = Data.Lessons.GetValueOrDefault(g.FirstLesson);
                var body = new StackPanel();
                var item = new AccessibleItem { Content = body, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(6, 4, 6, 4) };
                body.Children.Add(Ui.Text(term, "FontBase", "Text", FontWeights.SemiBold));
                body.Children.Add(Ui.Muted(g.Definition));
                if (lesson != null)
                {
                    var link = Ui.Btn($"First in: {lesson.Meta.Title}", () => Go($"#/lesson/{lesson.Meta.Id}"), "BtnFlat");
                    link.HorizontalAlignment = HorizontalAlignment.Left;
                    link.Padding = new Thickness(0, 2, 0, 2);
                    link.MinHeight = 0;
                    body.Children.Add(link);
                }
                else body.Children.Add(Ui.Text($"First in: {g.FirstLesson}", "FontSmall", "TextFaint"));
                System.Windows.Automation.AutomationProperties.SetName(item, $"{term}: {g.Definition}");
                Menus.Attach(item, _ =>
                [
                    new MenuEntry($"Copy \"{term}\"", () => Menus.Copy(term), Icons.Copy),
                    new MenuEntry("Copy definition", () => Menus.Copy(g.Definition), Icons.Copy),
                    new MenuEntry("Copy term and definition", () => Menus.Copy($"{term}: {g.Definition}"), Icons.Copy),
                    null,
                    new MenuEntry(lesson != null ? $"Open \"{lesson.Meta.Title}\"" : "Lesson not in your library", lesson != null ? () => Go($"#/lesson/{lesson.Meta.Id}") : null, Icons.Page),
                    new MenuEntry($"Search the web for \"{term}\"", () => Menus.SearchWeb($"{term} game development"), Icons.Search),
                ]);
                stack.Children.Add(item);
                if (focusTerm != null && string.Equals(term, focusTerm, StringComparison.OrdinalIgnoreCase)) highlighted = item;
            }
        }
        root = Ui.Page(stack, 640);
        Menus.Attach(root, _ => entries.Count > 0 ? [new MenuEntry("Copy whole glossary", () => Menus.Copy(string.Join('\n', entries.Select(kv => $"{kv.Key}: {kv.Value.Definition}"))), Icons.Copy)] : null, outermost: true);
    }

    public override void Shown()
    {
        if (highlighted == null) return;
        highlighted.BringIntoView();
        highlighted.Focus();
        if (highlighted is Control c) c.Res(Control.BackgroundProperty, "Surface2");
    }
}
