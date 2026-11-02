using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;
using Nordlys.Core.Quiz;

namespace Nordlys.Tests;

public class QuizTests
{
    private static T With<T>(T q) where T : Question => q with { Hints = ["h1", "h2"] };

    private static readonly McqQuestion Mcq = With(new McqQuestion { Id = "q1", Prompt = "x", Explanation = "x", Choices = ["a", "b", "c"], Answer = "B", Feedback = new Dictionary<string, string> { ["A"] = "Not A because…" } });

    [Fact]
    public void Mcq_checks_the_letter_and_gives_feedback()
    {
        Assert.Equal(new CheckResult(true), Checks.Mcq(Mcq, "B"));
        Assert.True(Checks.Mcq(Mcq, "b").Correct);
        Assert.Equal(new CheckResult(false), Checks.Mcq(Mcq, "C"));
        Assert.Equal(new CheckResult(false, "Not A because…"), Checks.Mcq(Mcq, "A"));
        Assert.Equal("**B.** b", Checks.AnswerText(Mcq));
    }

    [Fact]
    public void Multi_needs_the_exact_set()
    {
        var q = new MultiQuestion { Id = "q1", Prompt = "x", Explanation = "x", Choices = ["a", "b", "c", "d"], Answer = ["A", "C"] };
        Assert.True(Checks.Multi(q, ["C", "A"]).Correct);
        Assert.False(Checks.Multi(q, ["A"]).Correct);
        Assert.False(Checks.Multi(q, ["A", "C", "D"]).Correct);
        Assert.False(Checks.Multi(q, []).Correct);
    }

    [Fact]
    public void Numeric_tolerance_comma_and_unit()
    {
        var q = new NumericQuestion { Id = "q1", Prompt = "x", Explanation = "x", Answer = 2.5, Tolerance = 0.1, Unit = "px" };
        Assert.True(Checks.Numeric(q, "2.5").Correct);
        Assert.True(Checks.Numeric(q, "2.6").Correct);
        Assert.True(Checks.Numeric(q, "2.4").Correct);
        Assert.False(Checks.Numeric(q, "2.7").Correct);
        Assert.True(Checks.Numeric(q, "2,5").Correct);
        Assert.True(Checks.Numeric(q, " 2.5 px ").Correct);
        var exact = q with { Answer = 5, Tolerance = 0 };
        Assert.True(Checks.Numeric(exact, "5").Correct);
        Assert.False(Checks.Numeric(exact, "5.01").Correct);
        Assert.Contains("not a number", Checks.Numeric(q, "five").Feedback);
        Assert.Null(Checks.ParseNumber(""));
        Assert.Equal(-3, Checks.ParseNumber("-3"));
        Assert.Equal(0.5, Checks.ParseNumber(".5"));
        Assert.Equal(1000, Checks.ParseNumber("1e3"));
    }

    [Fact]
    public void Text_normalises_whitespace_and_case()
    {
        var q = new TextQuestion { Id = "q1", Prompt = "x", Explanation = "x", Accept = ["Core loop", "loop"] };
        Assert.True(Checks.Text(q, "  core   LOOP ").Correct);
        Assert.True(Checks.Text(q, "loop").Correct);
        Assert.False(Checks.Text(q, "core-loop").Correct);
        var cs = q with { CaseSensitive = true };
        Assert.True(Checks.Text(cs, "Core loop").Correct);
        Assert.False(Checks.Text(cs, "core loop").Correct);
        Assert.Equal("a b c", Checks.Normalize("  A \t B\n C "));
    }

    [Fact]
    public void Fill_needs_every_blank()
    {
        var q = new FillQuestion { Id = "q1", Prompt = "`func ___():` and `.___ = 1`", Explanation = "x", Blanks = [["_ready", "_ready()"], ["position"]] };
        Assert.True(Checks.Fill(q, ["_ready", "position"]).Correct);
        Assert.True(Checks.Fill(q, [" _ready() ", "POSITION"]).Correct);
        Assert.False(Checks.Fill(q, ["_ready", "pos"]).Correct);
        Assert.False(Checks.Fill(q, ["_ready"]).Correct);
    }

    [Fact]
    public void Order_and_shuffle()
    {
        var q = new OrderQuestion { Id = "q1", Prompt = "x", Explanation = "x", Items = ["one", "two", "three"] };
        Assert.True(Checks.Order(q, ["one", "two", "three"]).Correct);
        Assert.False(Checks.Order(q, ["two", "one", "three"]).Correct);
        Assert.False(Checks.Order(q, ["one", "two"]).Correct);
        for (var i = 0; i < 50; i++) Assert.NotEqual(["a", "b", "c"], Checks.Shuffled(["a", "b", "c"], true));
    }

    [Fact]
    public void Match_needs_every_pair()
    {
        var q = new MatchQuestion { Id = "q1", Prompt = "x", Explanation = "x", Pairs = [("a", "1"), ("b", "2"), ("c", "3")] };
        Assert.True(Checks.Match(q, ["1", "2", "3"]).Correct);
        Assert.False(Checks.Match(q, ["1", "3", "2"]).Correct);
        Assert.False(Checks.Match(q, ["1", null, "3"]).Correct);
    }

    private static readonly TextQuestion T = With(new TextQuestion { Id = "q1", Prompt = "x", Explanation = "x", Accept = ["x"] });

    [Fact]
    public void Attempt_first_try_is_correct()
    {
        var a = new Attempt(T);
        Assert.Equal(Outcome.Correct, a.Submit(true));
        Assert.Equal(Results.Correct, a.Result);
        Assert.True(a.Done);
    }

    [Fact]
    public void Attempt_after_hint_or_second_try_is_half()
    {
        var a = new Attempt(T);
        Assert.Equal("h1", a.NextHint());
        Assert.Equal(1, a.HintsLeft);
        a.Submit(true);
        Assert.Equal(Results.CorrectHinted, a.Result);

        var b = new Attempt(T);
        Assert.Equal(Outcome.NotYet, b.Submit(false));
        Assert.False(b.Done);
        Assert.Equal(Outcome.Correct, b.Submit(true));
        Assert.Equal(Results.CorrectHinted, b.Result);
    }

    [Fact]
    public void Attempt_wrong_twice_or_show_answer_is_wrong()
    {
        var a = new Attempt(T);
        a.Submit(false);
        Assert.Equal(Outcome.Revealed, a.Submit(false));
        Assert.Equal(Results.Wrong, a.Result);
        Assert.Throws<InvalidOperationException>(() => a.Submit(true));
        var b = new Attempt(T);
        b.Reveal();
        Assert.Equal(Results.Wrong, b.Result);
    }

    [Fact]
    public void Attempt_hints_run_out_and_self_rating_maps()
    {
        var a = new Attempt(T);
        a.NextHint();
        a.NextHint();
        Assert.Null(a.NextHint());
        foreach (var (r, want) in new[] { (SelfRating.Got, Results.SelfGot), (SelfRating.Partly, Results.SelfPartly), (SelfRating.Not, Results.SelfNot) })
        {
            var x = new Attempt(T);
            x.SelfRate(r);
            Assert.Equal(want, x.Result);
        }
    }

    [Fact]
    public void ScoreRun_leaves_tasks_out()
    {
        Assert.Equal(new RunScore(3, 6, 0.5), Checks.ScoreRun(["correct", "correct-hinted", "wrong", "task", "self-got", "self-partly", "self-not"]));
        Assert.Equal(new RunScore(0, 0, 0), Checks.ScoreRun(["task"]));
    }
}
