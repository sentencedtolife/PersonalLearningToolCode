// Lesson format version 1. See content/generator-prompt.md (FORMAT SPEC): the two must match.
namespace Nordlys.Core.Lesson;

public static class Format
{
    public const int Version = 1;
    public static readonly string[] Tracks = ["C", "X", "P", "D", "A", "S", "N", "K"];
    public static readonly string[] PaperTypes = ["concept", "worked-example", "case-study", "brief", "reference"];
    public static readonly string[] QuestionTypes = ["mcq", "multi", "numeric", "text", "fill", "order", "match", "reflect", "task"];

    public static string Letter(int i) => ((char)('A' + i)).ToString();
    public static int LetterIndex(string l) => l.Length == 1 && char.IsLetter(l[0]) ? char.ToUpperInvariant(l[0]) - 'A' : -1;
}

public sealed record GlossaryEntry(string Term, string Definition);
public sealed record Seed(string? Theme, string? Verb, string? Constraint);

public sealed record LessonMeta
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Track { get; init; }
    public required string Unit { get; init; }
    public required int Tier { get; init; }
    public required int Energy { get; init; }
    public required int Minutes { get; init; }
    public required string PaperType { get; init; }
    public required string DoneMeans { get; init; }
    public required string Summary { get; init; }
    public IReadOnlyList<string> Prerequisites { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<GlossaryEntry> Glossary { get; init; } = [];
    public Seed? Seed { get; init; }
    public int Version { get; init; } = Format.Version;
}

public abstract record Question
{
    public required string Id { get; init; }
    public required string Prompt { get; init; }
    public IReadOnlyList<string> Hints { get; init; } = [];
    public required string Explanation { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    /// <summary>True if the block comes after the "## Practice" heading.</summary>
    public bool InPractice { get; init; }
    /// <summary>1-based line of the opening ~~~question fence.</summary>
    public int Line { get; init; }
    public abstract string Type { get; }
}

public sealed record McqQuestion : Question
{
    public override string Type => "mcq";
    public required IReadOnlyList<string> Choices { get; init; }
    public required string Answer { get; init; }
    public IReadOnlyDictionary<string, string> Feedback { get; init; } = new Dictionary<string, string>();
}

public sealed record MultiQuestion : Question
{
    public override string Type => "multi";
    public required IReadOnlyList<string> Choices { get; init; }
    public required IReadOnlyList<string> Answer { get; init; }
}

public sealed record NumericQuestion : Question
{
    public override string Type => "numeric";
    public required double Answer { get; init; }
    public double Tolerance { get; init; }
    public string? Unit { get; init; }
}

public sealed record TextQuestion : Question
{
    public override string Type => "text";
    public required IReadOnlyList<string> Accept { get; init; }
    public bool CaseSensitive { get; init; }
}

public sealed record FillQuestion : Question
{
    public override string Type => "fill";
    public required IReadOnlyList<IReadOnlyList<string>> Blanks { get; init; }
    public bool CaseSensitive { get; init; }
}

public sealed record OrderQuestion : Question
{
    public override string Type => "order";
    public required IReadOnlyList<string> Items { get; init; }
}

public sealed record MatchQuestion : Question
{
    public override string Type => "match";
    public required IReadOnlyList<(string Left, string Right)> Pairs { get; init; }
}

public sealed record ReflectQuestion : Question
{
    public override string Type => "reflect";
    public required string ModelAnswer { get; init; }
    public IReadOnlyList<string> Rubric { get; init; } = [];
}

public sealed record TaskQuestion : Question
{
    public override string Type => "task";
    public required string Instructions { get; init; }
    public required string TaskDoneMeans { get; init; }
}

public sealed record LessonDoc(LessonMeta Meta, string Body, IReadOnlyList<Question> Questions);

public enum Severity { Error, Warning }

public sealed record Issue(Severity Severity, int Line, string Field, string Message, string Fix);
