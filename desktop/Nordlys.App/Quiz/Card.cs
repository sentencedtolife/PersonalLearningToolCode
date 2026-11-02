// One question on screen: prompt, answer control, Check / Hint / Show answer, feedback, Next.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nordlys.App.UI;
using Nordlys.Core.Lesson;
using Nordlys.Core.Quiz;

namespace Nordlys.App.Quiz;

public sealed class QuestionCard
{
    private readonly Question q;
    private readonly IAnswerWidget widget;
    private readonly StackPanel hints = new();
    private readonly ContentControl feedback = new();
    private readonly WrapPanel actions = new() { Margin = new Thickness(0, 20, 0, 0) };
    private readonly string? nextLabel;
    private readonly Action? onNext;
    private readonly Action<string>? onDone;
    private bool modelShown;
    private bool nudgeShown;

    public Attempt Attempt { get; }
    public Border Element { get; }
    public Question Question => q;
    public bool CanHint => Attempt.HintsLeft > 0 && q is not ReflectQuestion;
    public bool CanShowAnswer => !Attempt.Done && q is not (ReflectQuestion or TaskQuestion);

    public QuestionCard(Question q, string? meta = null, string? nextLabel = null, Action? onNext = null, Action<string>? onDone = null, Action<bool>? onTaskCheck = null)
    {
        this.q = q;
        this.nextLabel = nextLabel;
        this.onNext = onNext;
        this.onDone = onDone;
        Attempt = new Attempt(q);
        widget = Factory.For(q, () =>
        {
            if (q is TaskQuestion) onTaskCheck?.Invoke(widget!.Value() is true);
            else if (!Attempt.Done) { widget!.ClearMark(); if (nudgeShown) { feedback.Content = null; nudgeShown = false; } }
        }, Check);
        if (widget is ChoiceWidget cw) cw.Correct = q switch { McqQuestion m => [m.Answer], MultiQuestion m => m.Answer, _ => [] };

        var body = new StackPanel();
        if (meta != null) body.Children.Add(Ui.With(Ui.Muted(meta, "FontTiny"), t => t.Margin = new Thickness(0, 0, 0, 8)));
        if (!widget.OwnsPrompt) body.Children.Add(Rich.Md(q.Prompt));
        body.Children.Add(widget.Element);
        body.Children.Add(hints);
        body.Children.Add(feedback);
        body.Children.Add(actions);
        Element = Ui.Card(body, "PadQ");
        Element.Tag = this; // context menus find the card from any element inside it
        Draw();
    }

    private static Border FeedbackBox(string kind, string title, params string?[] parts)
    {
        var titleText = Ui.Text(title, "FontBase", kind == "correct" ? "Correct" : kind == "notyet" ? "NotYet" : "Accent", FontWeights.Bold);
        var stack = new StackPanel { Children = { titleText } };
        foreach (var p in parts.OfType<string>().Where(p => p != "")) stack.Children.Add(Ui.With(Rich.Md(p), r => r.Margin = new Thickness(0, 6, 0, 0)));
        var b = new Border { Child = stack, BorderThickness = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 16, 0, 0) };
        return b.Res(Border.BackgroundProperty, "Bg").Res(Border.BorderBrushProperty, kind == "correct" ? "Correct" : kind == "notyet" ? "NotYet" : "Accent");
    }

    private void Finish()
    {
        widget.Lock();
        onDone?.Invoke(Attempt.Result!);
        Draw();
        actions.Children.OfType<Button>().FirstOrDefault(b => b.Style == Application.Current.FindResource("BtnPrimary"))?.Focus();
    }

    public void Check()
    {
        if (Attempt.Done || q is ReflectQuestion or TaskQuestion) return;
        var value = widget.Value();
        if (value == null)
        {
            feedback.Content = Ui.With(Ui.Muted("Choose or type an answer first.", "FontSmall"), t => t.Margin = new Thickness(0, 12, 0, 0));
            nudgeShown = true;
            return;
        }
        var r = q switch
        {
            McqQuestion m => Checks.Mcq(m, (string)value),
            MultiQuestion m => Checks.Multi(m, (List<string>)value),
            NumericQuestion n => Checks.Numeric(n, (string)value),
            TextQuestion t => Checks.Text(t, (string)value),
            FillQuestion f => Checks.Fill(f, (List<string>)value),
            OrderQuestion o => Checks.Order(o, (List<string>)value),
            MatchQuestion m => Checks.Match(m, (List<string?>)value),
            _ => new CheckResult(true),
        };
        nudgeShown = false;
        switch (Attempt.Submit(r.Correct))
        {
            case Outcome.Correct:
                widget.Mark(MarkState.Correct);
                feedback.Content = FeedbackBox("correct", "Correct", q.Explanation);
                Finish();
                break;
            case Outcome.NotYet:
                widget.Mark(MarkState.NotYet);
                var tail = Attempt.HintsLeft > 0 ? "Try again, or take a hint. You have 1 more try." : "Try again. You have 1 more try.";
                feedback.Content = FeedbackBox("notyet", "Not yet", r.Feedback, tail);
                Draw();
                break;
            default:
                widget.Mark(MarkState.Reveal);
                feedback.Content = FeedbackBox("info", "Here is the answer", r.Feedback, Checks.AnswerText(q), q.Explanation);
                Finish();
                break;
        }
    }

    public void ShowHint()
    {
        if (!CanHint || Attempt.NextHint() is not { } hint) return;
        var box = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0) };
        box.Res(Border.BackgroundProperty, "Surface2");
        box.Child = new StackPanel { Children = { Ui.Text($"Hint {Attempt.HintsShown}", "FontSmall", "Text", FontWeights.Bold), Rich.Md(hint) } };
        hints.Children.Add(box);
        Draw();
    }

    public void ShowAnswer()
    {
        if (!CanShowAnswer) return;
        Attempt.Reveal();
        widget.Mark(MarkState.Reveal);
        feedback.Content = FeedbackBox("info", "Here is the answer", Checks.AnswerText(q), q.Explanation);
        Finish();
    }

    public void ShowModel()
    {
        if (modelShown || q is not ReflectQuestion) return;
        modelShown = true;
        widget.Lock();
        feedback.Content = FeedbackBox("info", "Model answer", Checks.AnswerText(q), q.Explanation);
        Draw();
        actions.Children.OfType<FrameworkElement>().SelectMany(Descendants).OfType<Button>().FirstOrDefault()?.Focus();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject d)
    {
        yield return d;
        if (d is Panel p) foreach (UIElement c in p.Children) foreach (var x in Descendants(c)) yield return x;
    }

    private void Rate(SelfRating r)
    {
        if (!modelShown || Attempt.Done) return;
        Attempt.SelfRate(r);
        Finish();
    }

    private void Next()
    {
        if (q is TaskQuestion && !Attempt.Done) { Attempt.CompleteTask(); onDone?.Invoke(Attempt.Result!); }
        onNext?.Invoke();
    }

    private void Draw()
    {
        actions.Children.Clear();
        void Add(UIElement e) { if (e is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(e); }
        Button? NextBtn() => nextLabel != null ? Ui.Btn(nextLabel, Next, "BtnPrimary", "Enter") : null;

        if (q is TaskQuestion || Attempt.Done)
        {
            if (Attempt.Done && q is ReflectQuestion)
            {
                var label = Attempt.Result switch { "self-got" => "Got it", "self-partly" => "Partly", _ => "Not yet" };
                Add(Ui.With(Ui.Muted($"You rated this: {label}", "FontSmall"), t => t.VerticalAlignment = VerticalAlignment.Center));
            }
            if (NextBtn() is { } nb) Add(nb);
            return;
        }
        if (q is ReflectQuestion)
        {
            if (!modelShown) { Add(Ui.Btn("Show model answer", ShowModel, "BtnPrimary", "Ctrl+Enter")); return; }
            Add(Ui.With(Ui.Muted("How did your answer compare?", "FontSmall"), t => t.VerticalAlignment = VerticalAlignment.Center));
            Add(Ui.Btn("Got it", () => Rate(SelfRating.Got), key: "1"));
            Add(Ui.Btn("Partly", () => Rate(SelfRating.Partly), key: "2"));
            Add(Ui.Btn("Not yet", () => Rate(SelfRating.Not), key: "3"));
            return;
        }
        Add(Ui.Btn("Check answer", Check, "BtnPrimary", "Enter"));
        if (CanHint) Add(Ui.Btn($"Hint ({Attempt.HintsLeft} left)", ShowHint, key: "H"));
        Add(Ui.Btn("Show answer", ShowAnswer, "BtnFlat"));
    }

    public void FocusFirst() => widget.FocusFirst();

    public bool OnKey(KeyEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        var mods = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Enter && (mods & ModifierKeys.Shift) == 0)
        {
            if ((mods & ModifierKeys.Control) != 0)
            {
                if (q is ReflectQuestion && !modelShown) { ShowModel(); return true; }
                return false;
            }
            if ((mods & ModifierKeys.Alt) != 0 || src is TextBox { AcceptsReturn: true }) return false;
            if (src is Button) return false; // let the focused button act
            if (q is TaskQuestion || Attempt.Done)
            {
                if (onNext != null) { Next(); return true; }
                return false;
            }
            if (q is ReflectQuestion) return false;
            Check();
            return true;
        }
        if (src is TextBox or ComboBox || mods != ModifierKeys.None) return false;
        if (key == Key.H && CanHint) { ShowHint(); return true; }
        if (modelShown && !Attempt.Done && key is Key.D1 or Key.D2 or Key.D3 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3)
        {
            Rate(key is Key.D1 or Key.NumPad1 ? SelfRating.Got : key is Key.D2 or Key.NumPad2 ? SelfRating.Partly : SelfRating.Not);
            return true;
        }
        return !Attempt.Done && widget.OnKey(e);
    }
}
