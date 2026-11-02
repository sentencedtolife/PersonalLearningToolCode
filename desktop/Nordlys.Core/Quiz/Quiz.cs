// One question attempt (2 tries, hints, reveal, self-rating), run scoring (HANDOFF 7.1), and answer checks.
using System.Globalization;
using System.Text.RegularExpressions;
using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;

namespace Nordlys.Core.Quiz;

public enum Outcome { Correct, NotYet, Revealed }
public enum SelfRating { Got, Partly, Not }

public sealed class Attempt(Question question)
{
    public const int MaxTries = 2;

    public Question Question { get; } = question;
    public int Tries { get; private set; }
    public int HintsShown { get; private set; }
    public string? Result { get; private set; }
    public bool Done => Result != null;
    public int HintsLeft => Done ? 0 : Question.Hints.Count - HintsShown;

    /// <summary>Reveals the next hint, or null if there are none left.</summary>
    public string? NextHint() => HintsLeft <= 0 ? null : Question.Hints[HintsShown++];

    /// <summary>Correct first time with no hints = correct; correct later = correct-hinted; 2 wrong tries = wrong.</summary>
    public Outcome Submit(bool correct)
    {
        if (Done) throw new InvalidOperationException("Question already answered");
        Tries++;
        if (correct)
        {
            Result = Tries == 1 && HintsShown == 0 ? Results.Correct : Results.CorrectHinted;
            return Outcome.Correct;
        }
        if (Tries >= MaxTries) { Result = Results.Wrong; return Outcome.Revealed; }
        return Outcome.NotYet;
    }

    /// <summary>"Show answer" counts as wrong.</summary>
    public void Reveal() => Result ??= Results.Wrong;

    public void SelfRate(SelfRating r) => Result = r switch { SelfRating.Got => Results.SelfGot, SelfRating.Partly => Results.SelfPartly, _ => Results.SelfNot };

    public void CompleteTask() => Result = Results.Task;
}

public sealed record RunScore(double Points, int Total, double Score);

public sealed record CheckResult(bool Correct, string? Feedback = null);

public static partial class Checks
{
    /// <summary>Tasks are not scored. Score is points / total (0–1); 0 if nothing was scored.</summary>
    public static RunScore ScoreRun(IEnumerable<string> results)
    {
        var scored = results.Where(r => r != Results.Task).ToList();
        var points = scored.Sum(Results.Score);
        return new RunScore(points, scored.Count, scored.Count > 0 ? points / scored.Count : 0);
    }

    /// <summary>Trim, collapse inner whitespace, and lowercase unless case-sensitive.</summary>
    public static string Normalize(string s, bool caseSensitive = false)
    {
        var t = Whitespace().Replace(s.Trim(), " ");
        return caseSensitive ? t : t.ToLowerInvariant();
    }

    public static bool MatchesAny(string input, IEnumerable<string> accepted, bool caseSensitive = false)
    {
        var n = Normalize(input, caseSensitive);
        return accepted.Any(a => Normalize(a, caseSensitive) == n);
    }

    /// <summary>Accepts "," or "." as the decimal separator, and ignores the unit if typed after the number.</summary>
    public static double? ParseNumber(string input, string? unit = null)
    {
        var s = input.Trim();
        if (!string.IsNullOrEmpty(unit) && s.EndsWith(unit, StringComparison.OrdinalIgnoreCase)) s = s[..^unit.Length].Trim();
        s = s.Replace(',', '.');
        if (!NumberRe().IsMatch(s)) return null;
        return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public static CheckResult Mcq(McqQuestion q, string letter)
    {
        var correct = letter.ToUpperInvariant() == q.Answer;
        return new(correct, correct ? null : q.Feedback.GetValueOrDefault(letter.ToUpperInvariant()));
    }

    public static CheckResult Multi(MultiQuestion q, IEnumerable<string> letters)
    {
        var want = q.Answer.Select(a => a.ToUpperInvariant()).ToHashSet();
        var got = letters.Select(a => a.ToUpperInvariant()).ToHashSet();
        return new(want.SetEquals(got));
    }

    public static CheckResult Numeric(NumericQuestion q, string input) =>
        ParseNumber(input, q.Unit) is { } n
            ? new(Math.Abs(n - q.Answer) <= q.Tolerance + 1e-9)
            : new(false, "That is not a number. Type digits only, like 3.5");

    public static CheckResult Text(TextQuestion q, string input) => new(MatchesAny(input, q.Accept, q.CaseSensitive));

    public static CheckResult Fill(FillQuestion q, IReadOnlyList<string> inputs) =>
        new(q.Blanks.Select((accepted, i) => MatchesAny(i < inputs.Count ? inputs[i] : "", accepted, q.CaseSensitive)).All(x => x));

    public static CheckResult Order(OrderQuestion q, IReadOnlyList<string> order) => new(order.SequenceEqual(q.Items));

    /// <summary>answer[i] is the right item chosen for left item i.</summary>
    public static CheckResult Match(MatchQuestion q, IReadOnlyList<string?> answer) =>
        new(q.Pairs.Select((p, i) => i < answer.Count && answer[i] == p.Right).All(x => x));

    /// <summary>Markdown describing the correct answer, shown on reveal.</summary>
    public static string AnswerText(Question q) => q switch
    {
        McqQuestion m => $"**{m.Answer}.** {m.Choices[Format.LetterIndex(m.Answer)]}",
        MultiQuestion m => string.Join('\n', m.Answer.Order().Select(l => $"- **{l}.** {m.Choices[Format.LetterIndex(l)]}")),
        NumericQuestion n => $"**{Yaml.NumberText(n.Answer)}{(n.Unit != null ? $" {n.Unit}" : "")}**"
            + (n.Tolerance > 0 ? $" (anything from {Yaml.NumberText(n.Answer - n.Tolerance)} to {Yaml.NumberText(n.Answer + n.Tolerance)} counts)" : ""),
        TextQuestion t => t.Accept.Count == 1 ? $"**{t.Accept[0]}**" : $"**{t.Accept[0]}** (also accepted: {string.Join(", ", t.Accept.Skip(1))})",
        FillQuestion f => string.Join('\n', f.Blanks.Select((b, i) => $"{i + 1}. **{b[0]}**")),
        OrderQuestion o => string.Join('\n', o.Items.Select((t, i) => $"{i + 1}. {t}")),
        MatchQuestion m => string.Join('\n', m.Pairs.Select(p => $"- {p.Left} → {p.Right}")),
        ReflectQuestion r => r.ModelAnswer + (r.Rubric.Count > 0 ? "\n\n**Check your answer against these points:**\n\n" + string.Join('\n', r.Rubric.Select(x => $"- {x}")) : ""),
        TaskQuestion t => t.TaskDoneMeans,
        _ => "",
    };

    /// <summary>Fisher–Yates. avoidOriginal re-shuffles (a few times) if the result equals the original order.</summary>
    public static List<T> Shuffled<T>(IReadOnlyList<T> items, bool avoidOriginal = false, Random? random = null)
    {
        random ??= Random.Shared;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var a = items.ToList();
            for (var i = a.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            if (!avoidOriginal || items.Count < 2 || !a.SequenceEqual(items)) return a;
        }
        return items.Reverse().ToList();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$")]
    private static partial Regex NumberRe();
}
