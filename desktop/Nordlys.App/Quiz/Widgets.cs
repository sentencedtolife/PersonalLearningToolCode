// Answer controls, one per question type. Checking is in Nordlys.Core (Checks); these only collect and show answers.
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using Nordlys.App.Rendering;
using Nordlys.App.UI;
using Nordlys.Core.Lesson;
using Nordlys.Core.Quiz;

namespace Nordlys.App.Quiz;

public enum MarkState { Correct, NotYet, Reveal }

public interface IAnswerWidget
{
    FrameworkElement Element { get; }
    /// <summary>True if the widget shows the prompt itself (fill puts the blanks inside it).</summary>
    bool OwnsPrompt => false;
    /// <summary>The current answer, or null if nothing has been entered yet.</summary>
    object? Value();
    void Lock();
    void Mark(MarkState s);
    void ClearMark();
    void FocusFirst();
    bool OnKey(KeyEventArgs e) => false;
}

public static class Rich
{
    /// <summary>A read-only, selectable document view that grows with its content.</summary>
    public static RichTextBox Box(FlowDocument doc)
    {
        var r = new RichTextBox { Document = doc, Focusable = true, IsTabStop = false };
        r.SetResourceReference(FrameworkElement.StyleProperty, typeof(RichTextBox));
        return r;
    }

    public static RichTextBox Md(string markdown, RenderOptions? o = null) => Box(Markdown.Document(markdown, o));

    public static TextBlock InlineMd(string markdown, string size = "FontBase")
    {
        var t = Ui.Text("", size);
        Markdown.InlinesInto(t.Inlines, markdown);
        return t;
    }
}

internal static class Factory
{
    public static IAnswerWidget For(Question q, Action changed, Action submit) => q switch
    {
        McqQuestion m => new ChoiceWidget(m.Choices, multiple: false, changed),
        MultiQuestion m => new ChoiceWidget(m.Choices, multiple: true, changed),
        NumericQuestion n => new InputWidget(changed, submit, n.Unit),
        TextQuestion => new InputWidget(changed, submit, null),
        FillQuestion f => new FillWidget(f, changed, submit),
        OrderQuestion o => new OrderWidget(o, changed),
        MatchQuestion m => new MatchWidget(m, changed),
        ReflectQuestion => new ReflectWidget(changed),
        TaskQuestion t => new TaskWidget(t, changed),
        _ => throw new NotSupportedException(q.Type),
    };
}

/// <summary>mcq and multi. Display order is shuffled; each choice keeps its letter. Keys 1–6 pick by position.</summary>
internal sealed class ChoiceWidget : IAnswerWidget
{
    private readonly bool multiple;
    private readonly List<(string Letter, ToggleButton Button)> buttons = [];
    private readonly Action changed;
    private readonly string group = Guid.NewGuid().ToString("N");
    public FrameworkElement Element { get; }

    public ChoiceWidget(IReadOnlyList<string> choices, bool multiple, Action changed)
    {
        this.multiple = multiple;
        this.changed = changed;
        var panel = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        if (multiple) panel.Children.Add(Ui.With(Ui.Muted("Select every correct choice.", "FontSmall"), t => t.Margin = new Thickness(0, 0, 0, 8)));
        var order = Checks.Shuffled(Enumerable.Range(0, choices.Count).ToList());
        for (var pos = 0; pos < order.Count; pos++)
        {
            var letter = Format.Letter(order[pos]);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = Ui.Text(letter, "FontBase", "TextMuted", FontWeights.SemiBold);
            l.Res(TextBlock.FontFamilyProperty, "MonoFont");
            l.Margin = new Thickness(0, 0, 12, 0);
            var text = Rich.InlineMd(choices[order[pos]]);
            Grid.SetColumn(text, 1);
            grid.Children.Add(l);
            grid.Children.Add(text);
            if (pos < 6)
            {
                var badge = Ui.KeyBadge((pos + 1).ToString());
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);
            }
            // One answer: radio buttons (one group per question). Several: toggle buttons. Both report their state to
            // screen readers, and react to it however it changes (click, Space, keys 1–6, UI Automation).
            ToggleButton b = multiple ? new ToggleButton() : new RadioButton { GroupName = group };
            b.Content = grid;
            b.Margin = new Thickness(0, 0, 0, 8);
            b.Res(FrameworkElement.StyleProperty, "Choice");
            System.Windows.Automation.AutomationProperties.SetName(b, $"{letter}. {choices[order[pos]]}");
            b.Checked += (_, _) => changed();
            b.Unchecked += (_, _) => changed();
            buttons.Add((letter, b));
            panel.Children.Add(b);
        }
        Element = panel;
    }

    public object? Value()
    {
        var sel = buttons.Where(b => b.Button.IsChecked == true).Select(b => b.Letter).Order().ToList();
        if (sel.Count == 0) return null;
        return multiple ? sel : sel[0];
    }

    public void Lock() { foreach (var (_, b) in buttons) b.IsHitTestVisible = b.Focusable = false; }

    public IReadOnlyList<string> Correct { get; set; } = [];

    public void Mark(MarkState s)
    {
        foreach (var (l, b) in buttons)
        {
            b.Tag = null;
            var sel = b.IsChecked == true;
            var right = Correct.Contains(l);
            if (s == MarkState.Correct && sel) b.Tag = "correct";
            if (s == MarkState.NotYet && sel) b.Tag = "notyet";
            if (s == MarkState.Reveal) b.Tag = right ? "correct" : sel ? "notyet" : null;
        }
    }

    public void ClearMark() { foreach (var (_, b) in buttons) b.Tag = null; }
    public void FocusFirst() => buttons.FirstOrDefault().Button?.Focus();

    public bool OnKey(KeyEventArgs e)
    {
        var n = e.Key switch { >= Key.D1 and <= Key.D6 => e.Key - Key.D1 + 1, >= Key.NumPad1 and <= Key.NumPad6 => e.Key - Key.NumPad1 + 1, _ => 0 };
        if (n == 0 || n > buttons.Count || Keyboard.Modifiers != ModifierKeys.None) return false;
        var (_, button) = buttons[n - 1];
        if (!button.IsHitTestVisible) return false;
        button.IsChecked = !multiple || button.IsChecked != true;
        button.Focus();
        return true;
    }
}

/// <summary>numeric and text: one line. Enter checks.</summary>
internal sealed class InputWidget : IAnswerWidget
{
    // IsReadOnly is inherited in WPF: inside a read-only document (inline cards, fill blanks) a text box would
    // silently become read-only too. Answer boxes set it explicitly.
    private readonly TextBox box = new() { MinWidth = 220, IsReadOnly = false };
    public FrameworkElement Element { get; }

    public InputWidget(Action changed, Action submit, string? unit)
    {
        box.TextChanged += (_, _) => changed();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { submit(); e.Handled = true; } };
        System.Windows.Automation.AutomationProperties.SetName(box, "Your answer");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        row.Children.Add(box);
        if (unit != null) row.Children.Add(Ui.With(Ui.Muted(unit), t => { t.Margin = new Thickness(8, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; }));
        Element = row;
    }

    public object? Value() => box.Text.Trim() == "" ? null : box.Text;
    public void Lock() => box.IsReadOnly = true;
    // A revealed answer leaves the box neutral: the box was not right, the answer is shown below.
    public void Mark(MarkState s) => box.Res(Control.BorderBrushProperty, s switch { MarkState.NotYet => "NotYet", MarkState.Correct => "Correct", _ => "Border" });
    public void ClearMark() => box.Res(Control.BorderBrushProperty, "Border");
    public void FocusFirst() => box.Focus();
}

/// <summary>fill: the prompt with a text box at each ___ (also inside code).</summary>
internal sealed partial class FillWidget : IAnswerWidget
{
    private readonly FillQuestion q;
    private readonly List<TextBox> inputs = [];
    public FrameworkElement Element { get; }
    public bool OwnsPrompt => true;

    public FillWidget(FillQuestion q, Action changed, Action submit)
    {
        this.q = q;
        var n = 0;
        // An identifier-like token survives Markdown and highlighting in one piece.
        var src = Validator.BlankPattern.Replace(q.Prompt, _ => $"ZZBLANK{n++}ZZ");
        var doc = Markdown.Document(src);
        var slots = new SortedDictionary<int, TextBox>();
        foreach (var run in AllRuns(doc.Blocks).ToList())
        {
            var matches = Token().Matches(run.Text);
            if (matches.Count == 0) continue;
            var parent = (InlineCollection?)(run.Parent switch { Paragraph p => p.Inlines, Span s => s.Inlines, _ => null });
            if (parent == null) continue;
            var text = run.Text;
            var last = 0;
            foreach (Match m in matches)
            {
                if (m.Index > last) parent.InsertBefore(run, CopyStyle(run, text[last..m.Index]));
                var i = int.Parse(m.Groups[1].Value);
                var tb = new TextBox { MinWidth = 110, Padding = new Thickness(4, 1, 4, 1), FontFamily = run.FontFamily, IsReadOnly = false };
                System.Windows.Automation.AutomationProperties.SetName(tb, $"Blank {i + 1}");
                tb.TextChanged += (_, _) => changed();
                tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { submit(); e.Handled = true; } };
                slots[i] = tb;
                parent.InsertBefore(run, new InlineUIContainer(tb) { BaselineAlignment = BaselineAlignment.Center });
                last = m.Index + m.Length;
            }
            if (last < text.Length) parent.InsertBefore(run, CopyStyle(run, text[last..]));
            parent.Remove(run);
        }
        inputs.AddRange(slots.Values);
        Element = Rich.Box(doc);
    }

    private static Run CopyStyle(Run from, string text)
    {
        var r = new Run(text) { Tag = from.Tag, FontStyle = from.FontStyle };
        if (from.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue) r.Foreground = from.Foreground;
        return r;
    }

    private static IEnumerable<Run> AllRuns(IEnumerable<Block> blocks)
    {
        foreach (var b in blocks)
        {
            if (b is Paragraph p) foreach (var r in Runs(p.Inlines)) yield return r;
            else if (b is List l) foreach (var li in l.ListItems) foreach (var r in AllRuns(li.Blocks)) yield return r;
            else if (b is Section s) foreach (var r in AllRuns(s.Blocks)) yield return r;
        }
    }

    private static IEnumerable<Run> Runs(InlineCollection inlines)
    {
        foreach (var i in inlines)
        {
            if (i is Run r) yield return r;
            else if (i is Span s) foreach (var x in Runs(s.Inlines)) yield return x;
        }
    }

    public object? Value() => inputs.Any(i => i.Text.Trim() != "") ? inputs.Select(i => i.Text).ToList() : null;
    public void Lock() { foreach (var i in inputs) i.IsReadOnly = true; }

    public void Mark(MarkState s)
    {
        for (var i = 0; i < inputs.Count; i++)
        {
            var ok = s != MarkState.NotYet || Checks.MatchesAny(inputs[i].Text, q.Blanks[i], q.CaseSensitive);
            if (s == MarkState.Reveal && !ok) inputs[i].Text = q.Blanks[i][0];
            inputs[i].Res(Control.BorderBrushProperty, ok ? "Correct" : "NotYet");
        }
    }

    public void ClearMark() { foreach (var i in inputs) i.Res(Control.BorderBrushProperty, "Border"); }
    public void FocusFirst() => inputs.FirstOrDefault()?.Focus();

    [GeneratedRegex(@"ZZBLANK(\d+)ZZ")]
    private static partial Regex Token();
}

/// <summary>order: drag, or focus an item, Space to pick it up, ↑/↓ to move it.</summary>
internal sealed class OrderWidget : IAnswerWidget
{
    private readonly OrderQuestion q;
    private readonly Action changed;
    private readonly StackPanel list = new();
    private List<string> current;
    private int? picked;
    private bool locked;
    public FrameworkElement Element { get; }

    public OrderWidget(OrderQuestion q, Action changed)
    {
        this.q = q;
        this.changed = changed;
        current = Checks.Shuffled(q.Items, avoidOriginal: true);
        var help = Ui.Muted("Drag the items, or select one (Space or click) and move it with ↑ and ↓.", "FontSmall");
        help.Margin = new Thickness(0, 0, 0, 8);
        Element = new StackPanel { Margin = new Thickness(0, 16, 0, 0), Children = { help, list } };
        Draw();
    }

    public IReadOnlyList<string> Current => current;

    private void Move(int from, int to)
    {
        if (locked || to < 0 || to >= current.Count || from == to) return;
        var item = current[from];
        current.RemoveAt(from);
        current.Insert(to, item);
        if (picked == from) picked = to;
        Draw(to);
        changed();
    }

    private void Draw(int? focus = null)
    {
        list.Children.Clear();
        for (var i = 0; i < current.Count; i++)
        {
            var index = i;
            var row = new DockPanel();
            var num = Ui.Text($"{i + 1}.", "FontBase", "TextMuted");
            num.Res(TextBlock.FontFamilyProperty, "MonoFont");
            num.Margin = new Thickness(0, 0, 10, 0);
            num.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(num, Dock.Left);
            var up = Ui.IconBtn(Icons.Up, "Move up", () => Move(index, index - 1));
            var down = Ui.IconBtn(Icons.Down, "Move down", () => Move(index, index + 1));
            foreach (var b in new[] { up, down }) { b.Width = b.Height = 28; b.IsTabStop = false; b.IsEnabled = !locked; DockPanel.SetDock(b, Dock.Right); }
            row.Children.Add(num);
            row.Children.Add(down);
            row.Children.Add(up);
            var text = Rich.InlineMd(current[i]);
            text.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(text);

            var item = new Border { Child = row, Padding = new Thickness(10, 6, 6, 6), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(picked == i ? 2 : 1), CornerRadius = new CornerRadius(8), Focusable = !locked, Cursor = locked ? Cursors.Arrow : Cursors.SizeAll, AllowDrop = true, Tag = i };
            item.Res(Border.BackgroundProperty, "Bg").Res(Border.BorderBrushProperty, picked == i ? "Accent" : "Border");
            item.FocusVisualStyle = (Style)Application.Current.FindResource("FocusRing");
            System.Windows.Automation.AutomationProperties.SetName(item, $"{i + 1}. {current[i]}{(picked == i ? " (selected)" : "")}");
            item.MouseLeftButtonDown += (_, e) => { if (locked || e.OriginalSource is TextBlock { Parent: Button }) return; pressAt = e.GetPosition(item); item.Focus(); };
            item.MouseLeftButtonUp += (_, _) => { if (!locked && !dragging) { picked = picked == index ? null : index; Draw(index); } };
            item.MouseMove += (_, e) =>
            {
                if (locked || e.LeftButton != MouseButtonState.Pressed || dragging || pressAt is not Point start) return;
                var d = e.GetPosition(item) - start;
                if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                dragging = true;
                pressAt = null;
                DragDrop.DoDragDrop(item, index.ToString(), DragDropEffects.Move);
                dragging = false;
            };
            item.Drop += (_, e) => { if (e.Data.GetData(typeof(string)) is string s && int.TryParse(s, out var from)) Move(from, index); };
            list.Children.Add(item);
        }
        if (focus is int f && f < list.Children.Count) list.Children[f].Focus();
    }

    private bool dragging;
    private Point? pressAt;

    private int FocusedIndex() => list.Children.IndexOf(list.Children.OfType<UIElement>().FirstOrDefault(c => c.IsKeyboardFocusWithin)!);

    public bool OnKey(KeyEventArgs e)
    {
        var i = FocusedIndex();
        if (i < 0 || locked) return false;
        if (e.Key == Key.Space) { picked = picked == i ? null : i; Draw(i); return true; }
        if (e.Key is Key.Up or Key.Down)
        {
            var to = e.Key == Key.Up ? i - 1 : i + 1;
            if (picked == i) Move(i, to);
            else if (to >= 0 && to < list.Children.Count) list.Children[to].Focus();
            return true;
        }
        return false;
    }

    public object? Value() => current.ToList();
    public void Lock() { locked = true; picked = null; Draw(); }

    public void Mark(MarkState s)
    {
        if (s == MarkState.Reveal) { current = q.Items.ToList(); Draw(); }
        for (var i = 0; i < list.Children.Count; i++)
            ((Border)list.Children[i]).Res(Border.BorderBrushProperty, s == MarkState.NotYet && current[i] != q.Items[i] ? "NotYet" : "Correct");
    }

    public void ClearMark() => Draw();
    public void FocusFirst() => (list.Children.Count > 0 ? list.Children[0] : null)?.Focus();
}

/// <summary>match: one dropdown per left item; the options are the right column, shuffled.</summary>
internal sealed class MatchWidget : IAnswerWidget
{
    private readonly MatchQuestion q;
    private readonly List<ComboBox> boxes = [];
    public FrameworkElement Element { get; }

    public MatchWidget(MatchQuestion q, Action changed)
    {
        this.q = q;
        var rights = Checks.Shuffled(q.Pairs.Select(p => p.Right).ToList(), avoidOriginal: true);
        var grid = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < q.Pairs.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var left = Rich.InlineMd(q.Pairs[i].Left);
            left.VerticalAlignment = VerticalAlignment.Center;
            left.Margin = new Thickness(0, 0, 12, 8);
            var cb = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
            cb.Items.Add(new ComboBoxItem { Content = "Choose…", Tag = null });
            foreach (var r in rights) cb.Items.Add(new ComboBoxItem { Content = r, Tag = r });
            cb.SelectedIndex = 0;
            cb.SelectionChanged += (_, _) => changed();
            System.Windows.Automation.AutomationProperties.SetName(cb, $"Match for {q.Pairs[i].Left}");
            Grid.SetRow(left, i);
            Grid.SetRow(cb, i);
            Grid.SetColumn(cb, 1);
            grid.Children.Add(left);
            grid.Children.Add(cb);
            boxes.Add(cb);
        }
        Element = grid;
    }

    private string? Selected(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag as string;

    public object? Value() => boxes.All(b => Selected(b) != null) ? boxes.Select(Selected).ToList() : null;
    public void Lock() { foreach (var b in boxes) b.IsEnabled = false; }

    public void Mark(MarkState s)
    {
        for (var i = 0; i < boxes.Count; i++)
        {
            if (s == MarkState.Reveal) boxes[i].SelectedItem = boxes[i].Items.OfType<ComboBoxItem>().First(x => (x.Tag as string) == q.Pairs[i].Right);
            var ok = s != MarkState.NotYet || Selected(boxes[i]) == q.Pairs[i].Right;
            boxes[i].Res(Control.BorderBrushProperty, ok ? "Correct" : "NotYet");
            boxes[i].BorderThickness = new Thickness(2);
        }
    }

    public void ClearMark() { foreach (var b in boxes) b.BorderThickness = new Thickness(0); }
    public void FocusFirst() => boxes.FirstOrDefault()?.Focus();
}

/// <summary>reflect: free text; the card shows the model answer and the self-rating.</summary>
internal sealed class ReflectWidget : IAnswerWidget
{
    private readonly TextBox box = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnly = false };
    public FrameworkElement Element { get; }

    public ReflectWidget(Action changed)
    {
        box.TextChanged += (_, _) => changed();
        box.Margin = new Thickness(0, 16, 0, 0);
        System.Windows.Automation.AutomationProperties.SetName(box, "Your answer. Ctrl+Enter shows the model answer.");
        System.Windows.Automation.AutomationProperties.SetHelpText(box, "Ctrl+Enter shows the model answer.");
        Element = box;
    }

    public object? Value() => box.Text;
    public void Lock() => box.IsReadOnly = true;
    public void Mark(MarkState s) { }
    public void ClearMark() { }
    public void FocusFirst() => box.Focus();
}

/// <summary>task: something to do outside the app. "I did this"; not scored, not reviewed.</summary>
internal sealed class TaskWidget : IAnswerWidget
{
    private readonly CheckBox box = new() { Content = "I did this" };
    public FrameworkElement Element { get; }

    public TaskWidget(TaskQuestion q, Action changed)
    {
        box.Click += (_, _) => changed();
        var steps = Rich.Md(q.Instructions);
        var done = Ui.DoneBox(q.TaskDoneMeans);
        done.Margin = new Thickness(0, 12, 0, 12);
        Element = new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { steps, done, box } };
    }

    public object? Value() => box.IsChecked == true;
    public void Lock() { }
    public void Mark(MarkState s) { }
    public void ClearMark() { }
    public void FocusFirst() => box.Focus();
}
