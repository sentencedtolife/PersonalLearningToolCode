// Lesson screen: Paper (reading + inline "Check yourself" cards) and Practice.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nordlys.App.Quiz;
using Nordlys.App.Rendering;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public static class LessonView
{
    private static readonly Dictionary<string, string> PaperTypeNames = new()
    {
        ["concept"] = "Concept", ["worked-example"] = "Worked example", ["case-study"] = "Case study", ["brief"] = "Brief", ["reference"] = "Reference",
    };

    public static View Create(string id, bool practice)
    {
        if (!Data.Lessons.TryGetValue(id, out var lesson))
            return new MessageView("Lesson not found", $"There is no lesson with the id \"{id}\" in your library.");
        Data.OpenLesson(lesson);
        var lp = Data.Progress.Lesson(id);
        lp.LastPosition = new Position { View = practice ? "practice" : "paper", Scroll = lp.LastPosition?.Scroll ?? 0 };
        return practice ? new PracticeView(lesson) : new PaperView(lesson);
    }

    public static Border Toggle(string id, bool practice) => Ui.Segmented(
        ("Paper", !practice, () => { if (practice) Replace($"#/lesson/{id}"); }),
        ("Practice", practice, () => { if (!practice) Replace($"#/lesson/{id}/practice"); }));

    public static StackPanel Header(LessonDoc lesson, bool full = true)
    {
        var m = lesson.Meta;
        var stripe = new Border { Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 0, 16) };
        stripe.Res(Border.BackgroundProperty, $"Track{m.Track}");
        var chips = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) };
        chips.Children.Add(Ui.Chip(Data.Curriculum.TrackName(m.Track), $"Track{m.Track}"));
        if (Data.Curriculum.UnitById(m.Unit) is { } unit) chips.Children.Add(Ui.Chip(unit.Title));
        foreach (var c in new[] { $"Tier {m.Tier}", $"Energy {m.Energy}", $"~{m.Minutes} min", PaperTypeNames.GetValueOrDefault(m.PaperType, m.PaperType) }) chips.Children.Add(Ui.Chip(c));
        var panel = new StackPanel { Children = { stripe, Ui.With(Ui.H1(m.Title), t => t.Margin = new Thickness(0, 0, 0, 4)), chips, Ui.DoneBox(m.DoneMeans) } };
        if (!full) return panel;

        if (m.Glossary.Count > 0)
        {
            var terms = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            terms.Children.Add(Ui.With(Ui.Muted("Terms in this lesson:", "FontSmall"), t => { t.Margin = new Thickness(0, 2, 8, 6); t.VerticalAlignment = VerticalAlignment.Center; }));
            foreach (var g in m.Glossary)
            {
                var b = Ui.Btn(g.Term, () => Go($"#/glossary?term={Uri.EscapeDataString(g.Term.ToLowerInvariant())}"));
                b.ToolTip = g.Definition;
                b.MinHeight = 0;
                b.Padding = new Thickness(8, 1, 8, 1);
                b.FontSize = 12;
                b.Margin = new Thickness(0, 0, 6, 6);
                ToolTipService.SetShowOnDisabled(b, true);
                terms.Children.Add(b);
            }
            panel.Children.Add(terms);
        }
        var levels = Data.Levels();
        var prereqs = m.Prerequisites.Select(p => Data.Lessons.GetValueOrDefault(p)?.Meta.Title ?? Data.Curriculum.UnitById(p)?.Title ?? p).ToList();
        if (prereqs.Count > 0)
        {
            var missing = NextLesson.MissingPrerequisites(lesson, Data.Lessons, levels);
            panel.Children.Add(Ui.With(Ui.Muted($"Builds on: {string.Join(", ", prereqs)}.{(missing.Count > 0 ? $" (Not in your library yet: {string.Join(", ", missing)}.)" : "")}", "FontSmall"), t => t.Margin = new Thickness(0, 8, 0, 0)));
        }
        return panel;
    }

    /// <summary>Right-click actions every lesson screen offers, after the context-specific ones.</summary>
    public static IEnumerable<MenuEntry?> LessonActions(LessonDoc lesson, bool practice)
    {
        var id = lesson.Meta.Id;
        var file = LessonFile(id);
        yield return null;
        yield return practice
            ? new MenuEntry("Read the paper", () => Replace($"#/lesson/{id}"), Icons.Page)
            : new MenuEntry($"Start practice ({lesson.Questions.Count} questions)", lesson.Questions.Count > 0 ? () => Replace($"#/lesson/{id}/practice") : null, Icons.Play, "P");
        yield return new MenuEntry("Copy lesson title", () => Menus.Copy(lesson.Meta.Title), Icons.Copy);
        yield return new MenuEntry("Ask Claude for a follow-up lesson", () => Go($"#/ask?request={Uri.EscapeDataString($"A follow-up to the lesson \"{lesson.Meta.Title}\" ({id}).")}"), Icons.Ask);
        yield return null;
        yield return new MenuEntry("Open lesson file in editor", file != null ? () => OpenInEditor(file) : null, Icons.Edit);
        yield return new MenuEntry("Show lesson file in Explorer", file != null ? () => ShowInExplorer(file) : null, Icons.Folder);
        yield return new MenuEntry("Reload lesson from file", () => ReloadAsync().Forget(), Icons.Refresh, "F5");
    }

    public static async Task ReloadAsync()
    {
        await Data.ReloadLessonsAsync();
        Reload();
    }

    /// <summary>Card menu: question actions, plus text actions if the right-click was on selectable text.</summary>
    public static IEnumerable<MenuEntry?> CardMenu(QuestionCard card, ContextMenuEventArgs e)
    {
        var list = new List<MenuEntry?>();
        if (e.Source is RichTextBox rtb) { list.AddRange(Menus.ForDocument(rtb, t => Go($"#/glossary?term={Uri.EscapeDataString(t)}"))); list.Add(null); }
        list.Add(new MenuEntry("Copy question", () => Menus.Copy(card.Question.Prompt.Trim()), Icons.Copy));
        if (card.CanHint) list.Add(new MenuEntry($"Show hint ({card.Attempt.HintsLeft} left)", card.ShowHint, Icons.Hint, "H"));
        if (card.CanShowAnswer) list.Add(new MenuEntry("Show answer", card.ShowAnswer, Icons.Show));
        if (card.Attempt.Done && card.Question.Explanation != "") list.Add(new MenuEntry("Copy explanation", () => Menus.Copy(card.Question.Explanation.Trim()), Icons.Copy));
        return list;
    }

    public static QuestionCard? CardFrom(object? source)
    {
        for (var d = source as DependencyObject; d != null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is Border { Tag: QuestionCard c }) return c;
        return null;
    }
}

internal sealed class PaperView : View
{
    private readonly LessonDoc lesson;
    private readonly ScrollViewer scroll;
    private readonly List<QuestionCard> cards = [];
    private readonly DispatcherTimer saveScroll = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public override string Title => lesson.Meta.Title;
    public override FrameworkElement Root => scroll;
    public override IEnumerable<UIElement> Actions => [LessonView.Toggle(lesson.Meta.Id, false)];

    public PaperView(LessonDoc lesson)
    {
        this.lesson = lesson;
        var id = lesson.Meta.Id;
        var practiceIds = lesson.Questions.Where(q => q.InPractice).Select(q => q.Id).ToHashSet();
        // Practice questions live in Practice mode; an empty "## Practice" section is dropped.
        var lines = lesson.Body.Split('\n').Where(l => !(Parser.Placeholder.Match(l) is { Success: true } m && practiceIds.Contains(m.Groups[1].Value))).ToList();
        var pi = lines.FindIndex(l => Parser.PracticeHeading.IsMatch(l));
        if (pi >= 0 && lines.Skip(pi + 1).All(l => l.Trim() == "")) lines.RemoveRange(pi, lines.Count - pi);

        var doc = Markdown.Document(string.Join('\n', lines), new Rendering.RenderOptions
        {
            Paper = true,
            Glossary = lesson.Meta.Glossary,
            Slot = qid =>
            {
                var q = lesson.Questions.FirstOrDefault(x => x.Id == qid);
                if (q == null) return null;
                var card = new QuestionCard(q, "Check yourself");
                Menus.Attach(card.Element, e => LessonView.CardMenu(card, e));
                cards.Add(card);
                return card.Element;
            },
        });
        var paper = Rich.Box(doc);
        paper.MaxWidth = 760; // ~72 characters
        paper.HorizontalAlignment = HorizontalAlignment.Left;
        Menus.Attach(paper, _ => Menus.ForDocument(paper, t => Go($"#/glossary?term={Uri.EscapeDataString(t)}")).Concat(LessonView.LessonActions(lesson, false)));

        var n = lesson.Questions.Count;
        var end = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 24, 0, 0), Margin = new Thickness(0, 40, 0, 0) };
        end.Res(Border.BorderBrushProperty, "Border");
        end.Child = n > 0
            ? Ui.With(Ui.Btn($"Start practice ({n} question{(n == 1 ? "" : "s")})", () => Go($"#/lesson/{id}/practice"), "BtnPrimary", "P"), b => b.HorizontalAlignment = HorizontalAlignment.Center)
            : Ui.Muted("This lesson has no questions.");

        var column = new StackPanel { MaxWidth = 760, Children = { LessonView.Header(lesson), Ui.With(paper, p => p.Margin = new Thickness(0, 24, 0, 0)), end } };
        scroll = Ui.Page(column);
        Menus.Attach(scroll, _ => LessonView.LessonActions(lesson, false).Skip(1), outermost: true);

        saveScroll.Tick += (_, _) =>
        {
            saveScroll.Stop();
            var max = scroll.ScrollableHeight;
            Data.Progress.Lesson(id).LastPosition = new Position { View = "paper", Scroll = max > 0 ? Math.Round(scroll.VerticalOffset / max, 3) : 0 };
            Data.SaveProgressAsync().Forget();
        };
        scroll.ScrollChanged += (_, e) => { if (e.VerticalChange != 0) { saveScroll.Stop(); saveScroll.Start(); } };
    }

    public override void Shown()
    {
        var pos = Data.Progress.Lesson(lesson.Meta.Id).LastPosition;
        if (pos is { View: "paper", Scroll: > 0 })
        {
            scroll.UpdateLayout();
            scroll.ScrollToVerticalOffset(pos.Scroll * scroll.ScrollableHeight);
        }
        scroll.Focus();
    }

    public override void Closed() => saveScroll.Stop();

    public override bool OnKey(KeyEventArgs e)
    {
        var card = cards.FirstOrDefault(c => c.Element.IsKeyboardFocusWithin);
        if (card?.OnKey(e) == true) return true;
        if (e.Key == Key.F5) { LessonView.ReloadAsync().Forget(); return true; }
        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.None && lesson.Questions.Count > 0 && Keyboard.FocusedElement is not TextBox) { Go($"#/lesson/{lesson.Meta.Id}/practice"); return true; }
        return false;
    }
}

/// <summary>One question per screen with progress dots. Used by Practice and Review.</summary>
internal sealed class QuestionRunner
{
    public sealed record Item(LessonDoc Lesson, Question Question, string Key);

    private readonly IReadOnlyList<Item> items;
    private readonly Dictionary<string, string> results;
    private readonly Func<Item, int, string> meta;
    private readonly Action<Item, string> onAnswer;
    private readonly Action<Item, bool>? onTask;
    private readonly Func<FrameworkElement> summary;
    public StackPanel Element { get; } = new();
    public QuestionCard? Card { get; private set; }
    public event Action? Changed;

    public QuestionRunner(IReadOnlyList<Item> items, Dictionary<string, string> results, Func<Item, int, string> meta, Action<Item, string> onAnswer, Func<FrameworkElement> summary, Action<Item, bool>? onTask = null)
    {
        this.items = items;
        this.results = results;
        this.meta = meta;
        this.onAnswer = onAnswer;
        this.onTask = onTask;
        this.summary = summary;
    }

    private FrameworkElement Dots(int current)
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 24) };
        for (var i = 0; i < items.Count; i++)
        {
            results.TryGetValue(items[i].Key, out var r);
            var key = r == null ? "Surface2" : r == Results.Task ? "TrackN" : Results.Score(r) switch { 1 => "Correct", 0 => "NotYet", _ => "TrackK" };
            var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Margin = new Thickness(3), BorderThickness = new Thickness(i == current ? 2 : 0), ToolTip = $"Question {i + 1}: {(r == null ? "not answered" : r == Results.Task ? "task" : Results.Score(r) switch { 1 => "correct", 0 => "not yet", _ => "partly" })}" };
            dot.Res(Border.BackgroundProperty, key).Res(Border.BorderBrushProperty, "Accent");
            row.Children.Add(dot);
        }
        System.Windows.Automation.AutomationProperties.SetName(row, $"Question {Math.Min(current, items.Count - 1) + 1} of {items.Count}");
        return row;
    }

    /// <summary>Resumes at the first unanswered question.</summary>
    public void Start()
    {
        var i = 0;
        while (i < items.Count && results.ContainsKey(items[i].Key)) i++;
        Show(i);
    }

    private void Show(int index)
    {
        Element.Children.Clear();
        Element.Children.Add(Dots(index));
        if (index >= items.Count)
        {
            Card = null;
            var s = summary();
            Element.Children.Add(s);
            Changed?.Invoke();
            return;
        }
        var item = items[index];
        var last = index == items.Count - 1;
        Card = new QuestionCard(item.Question, meta(item, index), last ? "See summary" : "Next question", () => Show(index + 1),
            r =>
            {
                results[item.Key] = r;
                onAnswer(item, r);
                // Panels can't replace a child by index; remove and insert instead.
                Element.Children.RemoveAt(0);
                Element.Children.Insert(0, Dots(index));
            },
            checkedNow => onTask?.Invoke(item, checkedNow));
        Element.Children.Add(Card.Element);
        Changed?.Invoke();
        Element.Dispatcher.BeginInvoke(() => Card?.FocusFirst(), DispatcherPriority.Input);
    }
}

internal sealed class PracticeView : View
{
    private readonly LessonDoc lesson;
    private readonly QuestionRunner runner;
    private readonly ScrollViewer scroll;

    public override string Title => lesson.Meta.Title;
    public override FrameworkElement Root => scroll;
    public override IEnumerable<UIElement> Actions => [LessonView.Toggle(lesson.Meta.Id, true)];

    public PracticeView(LessonDoc lesson)
    {
        this.lesson = lesson;
        var id = lesson.Meta.Id;
        var lp = Data.Progress.Lesson(id);
        lp.Run ??= new PracticeRun();
        var run = lp.Run;
        var items = lesson.Questions.Select(q => new QuestionRunner.Item(lesson, q, q.Id)).ToList();
        var anyTask = items.Any(i => i.Question is TaskQuestion);

        runner = new QuestionRunner(items, run.Results,
            (it, i) => $"Question {i + 1} of {items.Count}{(anyTask && it.Question is TaskQuestion ? " · task (not scored)" : "")}",
            (it, r) =>
            {
                if (r != Results.Task) Leitner.RecordAnswer(Data.Progress, $"{id}#{it.Question.Id}", r, Dates.Today());
                Data.SaveProgressAsync().Forget();
            },
            () => Summary(lp, items),
            (it, done) =>
            {
                var key = $"{id}#{it.Question.Id}";
                if (done) Data.Progress.TaskChecks[key] = Dates.Today(); else Data.Progress.TaskChecks.Remove(key);
                Data.SaveProgressAsync().Forget();
            });
        runner.Changed += () => { if (runner.Card is { } c) Menus.Attach(c.Element, e => LessonView.CardMenu(c, e).Concat(LessonView.LessonActions(lesson, true))); };

        var column = new StackPanel { MaxWidth = 760, Children = { LessonView.Header(lesson, full: false), Ui.With(runner.Element, r => r.Margin = new Thickness(0, 24, 0, 0)) } };
        scroll = Ui.Page(column);
        Menus.Attach(scroll, _ => LessonView.LessonActions(lesson, true).Skip(1), outermost: true);
        runner.Start();
    }

    private FrameworkElement Summary(LessonProgress lp, List<QuestionRunner.Item> items)
    {
        var id = lesson.Meta.Id;
        var run = lp.Run!;
        var s = Nordlys.Core.Quiz.Checks.ScoreRun(items.Select(i => run.Results.GetValueOrDefault(i.Key)).OfType<string>());
        if (!run.Finished)
        {
            run.Finished = true;
            lp.Attempts.Add(new AttemptRecord(Dates.Today(), Math.Round(s.Score, 2), s.Total));
            Data.SaveProgressAsync().Forget();
        }
        void MarkDone()
        {
            lp.Status = "practiced";
            lp.Run = null;
            lp.LastPosition = null;
            var minutes = Data.OpenedAt.TryGetValue(id, out var opened) ? (int)Math.Clamp(Math.Round((DateTime.Now - opened).TotalMinutes), 1, 240) : lesson.Meta.Minutes;
            Data.Progress.Sessions.Add(new SessionRecord(Dates.Today(), Data.Energy ?? lesson.Meta.Energy, id, minutes));
            Data.OpenedAt.Remove(id);
            Data.SaveProgressAsync().Forget();
            Go("#/home");
        }
        void Again()
        {
            lp.Run = new PracticeRun();
            Data.SaveProgressAsync().Forget();
            Reload();
        }
        var stack = Ui.Stack(
            Ui.With(Ui.H2("Practice finished"), t => t.HorizontalAlignment = HorizontalAlignment.Center),
            s.Total > 0 ? Ui.With(Ui.Text($"{Math.Round(s.Score * 100)}%", "FontH1", "Accent", FontWeights.Bold), t => t.HorizontalAlignment = HorizontalAlignment.Center) : null,
            s.Total > 0 ? Ui.With(Ui.Muted($"{Nordlys.Core.Lesson.Yaml.NumberText(s.Points)} out of {s.Total} point{(s.Total == 1 ? "" : "s")}. Correct first time = 1 point, with a hint or second try = ½ point."), t => t.TextAlignment = TextAlignment.Center) : null,
            Ui.DoneBox(lesson.Meta.DoneMeans),
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = { Ui.Btn("Mark lesson done", MarkDone, "BtnPrimary", "Enter"), Ui.Btn("Practice again", Again, "BtnFlat") } });
        var card = Ui.Card(stack, "PadQ");
        card.Loaded += (_, _) => (((StackPanel)stack.Children[^1]).Children[0] as Button)?.Focus();
        return card;
    }

    public override void Shown() { }
    public override bool OnKey(KeyEventArgs e)
    {
        if (runner.Card?.OnKey(e) == true) return true;
        if (e.Key == Key.F5) { LessonView.ReloadAsync().Forget(); return true; }
        return false;
    }
}

internal sealed class ReviewView : View
{
    private readonly FrameworkElement root;
    private readonly QuestionRunner? runner;
    public override string Title => "Review";
    public override FrameworkElement Root => root;

    public ReviewView()
    {
        var queue = Leitner.ReviewQueue(Data.Progress, Data.Lessons, Dates.Today());
        if (queue.Count == 0)
        {
            var next = Data.Progress.Questions.Values.Select(q => q.Due).Where(d => string.CompareOrdinal(d, Dates.Today()) > 0).Order(StringComparer.Ordinal).FirstOrDefault();
            var home = Ui.Btn("Back to Home", () => Go("#/home"), "BtnPrimary", "Enter");
            home.HorizontalAlignment = HorizontalAlignment.Center;
            home.Loaded += (_, _) => home.Focus();
            root = Ui.Page(Ui.Stack(
                Ui.With(Ui.H1("Nothing to review today"), t => t.HorizontalAlignment = HorizontalAlignment.Center),
                Ui.With(Ui.Muted(next != null ? $"The next questions come back on {next}." : "Questions appear here after you practise a lesson."), t => t.HorizontalAlignment = HorizontalAlignment.Center),
                home), 640);
            Menus.Attach(root, _ => [new MenuEntry("Back to Home", () => Go("#/home"), Icons.Home)], outermost: true);
            return;
        }
        var items = queue.Select(d => { var l = Data.Lessons[d.LessonId]; return new QuestionRunner.Item(l, l.Questions.First(q => q.Id == d.QuestionId), $"{d.LessonId}#{d.QuestionId}"); }).ToList();
        var results = new Dictionary<string, string>();
        runner = new QuestionRunner(items, results,
            (it, i) => $"Review {i + 1} of {items.Count} · from \"{it.Lesson.Meta.Title}\"",
            (it, r) => { if (r != Results.Task) Leitner.RecordAnswer(Data.Progress, it.Key, r, Dates.Today()); Data.SaveProgressAsync().Forget(); },
            () =>
            {
                var right = results.Values.Count(r => r != Results.Task && Results.Score(r) == 1);
                var total = results.Values.Count(r => r != Results.Task);
                var home = Ui.Btn("Back to Home", () => Go("#/home"), "BtnPrimary", "Enter");
                home.HorizontalAlignment = HorizontalAlignment.Center;
                home.Loaded += (_, _) => home.Focus();
                return Ui.Card(Ui.Stack(Ui.With(Ui.H2("Review finished"), t => t.HorizontalAlignment = HorizontalAlignment.Center),
                    Ui.With(Ui.Text($"{right} of {total} correct first time. Each question comes back later: sooner if it was hard, later if it was easy for you."), t => t.TextAlignment = TextAlignment.Center), home), "PadQ");
            });
        runner.Changed += () =>
        {
            if (runner.Card is not { } c) return;
            var item = items.First(i => i.Question == c.Question);
            Menus.Attach(c.Element, e => LessonView.CardMenu(c, e).Concat([null, new MenuEntry($"Open lesson \"{item.Lesson.Meta.Title}\"", () => Go($"#/lesson/{item.Lesson.Meta.Id}"), Icons.Page)]));
        };
        root = Ui.Page(runner.Element, 808);
        Menus.Attach(root, _ => [new MenuEntry("Back to Home", () => Go("#/home"), Icons.Home)], outermost: true);
        runner.Start();
    }

    public override bool OnKey(KeyEventArgs e) => runner?.Card?.OnKey(e) == true;
}
