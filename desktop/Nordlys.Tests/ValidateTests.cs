using Nordlys.Core.Lesson;

namespace Nordlys.Tests;

public class ValidateTests
{
    private static readonly (string Key, string Value)[] Meta =
    [
        ("id", "t-check-01"), ("title", "Check"), ("track", "C"), ("unit", "C.loops"), ("tier", "1"), ("energy", "1"),
        ("minutes", "5"), ("paper_type", "concept"), ("done_means", "\"Done.\""), ("summary", "\"A lesson for tests.\""), ("version", "1"),
    ];

    private const string DefaultQ = "id: q1\ntype: text\nprompt: \"Hi?\"\naccept: [\"hi\"]\nexplanation: \"x\"";

    /// <summary>overrides: key -> value (null removes the line); extra keys are appended.</summary>
    internal static string Lesson(IDictionary<string, string?>? overrides = null, string[]? questions = null)
    {
        overrides ??= new Dictionary<string, string?>();
        var lines = new List<string>();
        foreach (var (k, v) in Meta)
        {
            var value = overrides.TryGetValue(k, out var o) ? o : v;
            if (value != null) lines.Add($"{k}: {value}");
        }
        foreach (var (k, v) in overrides) if (Meta.All(m => m.Key != k) && v != null) lines.Add($"{k}: {v}");
        var qs = questions ?? [DefaultQ];
        return $"---\n{string.Join('\n', lines)}\n---\n\n# T\n\n## Practice\n\n{string.Join("\n\n", qs.Select(q => $"~~~question\n{q}\n~~~"))}\n";
    }

    internal static string Q(string type, string fields, string id = "q1") => $"id: {id}\ntype: {type}\nprompt: \"Question?\"\nexplanation: \"Because.\"\n{fields}";

    private static readonly HashSet<string> Units = ["C.loops", "C.verbs", "P.tour"];

    private static List<string> ErrorsOf(string text) => Validator.Load(text, Units).Errors.Select(e => $"{e.Field}: {e.Message}").ToList();
    private static List<string> WarningsOf(string text) => Validator.Load(text, Units).Warnings.Select(e => $"{e.Field}: {e.Message}").ToList();
    private static Dictionary<string, string?> M(string k, string? v) => new() { [k] = v };

    [Fact]
    public void Minimal_lesson_is_clean()
    {
        var r = Validator.Load(Lesson(), Units);
        Assert.Empty(r.Errors);
        Assert.Empty(r.Warnings);
        Assert.Equal("t-check-01", r.Lesson!.Meta.Id);
        var q = Assert.IsType<TextQuestion>(r.Lesson.Questions[0]);
        Assert.True(q.InPractice);
        Assert.False(q.CaseSensitive);
    }

    [Fact]
    public void Defaults_are_applied()
    {
        var r = Validator.Load(Lesson(null, [Q("numeric", "answer: 3"), Q("mcq", "choices: [\"a\", \"b\"]\nanswer: a", "q2")]));
        Assert.Empty(r.Errors);
        var n = Assert.IsType<NumericQuestion>(r.Lesson!.Questions[0]);
        Assert.Equal(0, n.Tolerance);
        Assert.Empty(n.Hints);
        var m = Assert.IsType<McqQuestion>(r.Lesson.Questions[1]);
        Assert.Equal("A", m.Answer);
        Assert.Empty(m.Feedback);
    }

    [Theory]
    [InlineData("summary", null, "summary: Missing required field \"summary\".")]
    [InlineData("id", "\"Has Spaces\"", "id: \"Has Spaces\" is not a valid id.")]
    [InlineData("track", "Design", "track: \"Design\" must be one of C, X, P, D, A, S, N (or K for capstones).")]
    [InlineData("tier", "5", "tier: 5 must be a whole number from 0 to 4.")]
    [InlineData("energy", "0", "energy: 0 must be a whole number from 1 to 3.")]
    [InlineData("minutes", "2.5", "minutes: 2.5 must be a whole number from 1 to 600.")]
    [InlineData("paper_type", "essay", "paper_type: \"essay\" must be one of concept, worked-example, case-study, brief, reference.")]
    [InlineData("version", "2", "version: version 2 is not supported. This app reads format version 1.")]
    [InlineData("title", "[1, 2]", "title: title must be text.")]
    [InlineData("unit", "42", "unit: unit must be text, e.g. C.loops.")]
    [InlineData("tags", "core-loop", "tags: tags must be a list of text items.")]
    [InlineData("prerequisites", "{a: 1}", "prerequisites: prerequisites must be a list of text items.")]
    [InlineData("glossary", "\"x\"", "glossary: glossary must be a list of {term, definition}.")]
    [InlineData("glossary", "[{term: x}]", "glossary[1]: Glossary entry 1 needs both \"term\" and \"definition\" as text.")]
    [InlineData("seed", "\"x\"", "seed: seed must be {theme, verb, constraint}.")]
    [InlineData("seed", "{theme: [a]}", "seed.theme: seed.theme must be text.")]
    public void Frontmatter_errors(string key, string? value, string expected) => Assert.Contains(expected, ErrorsOf(Lesson(M(key, value))));

    [Fact]
    public void Field_errors_point_at_the_field_line()
    {
        var e = Validator.Load(Lesson(M("track", "Design"))).Errors[0];
        Assert.Equal((4, "track"), (e.Line, e.Field));
    }

    [Fact]
    public void Unknown_unit_is_a_warning_only()
    {
        var r = Validator.Load(Lesson(M("unit", "C.nothing")), Units);
        Assert.Empty(r.Errors);
        Assert.Contains("\"C.nothing\" is not a unit in curriculum.json.", r.Warnings.Select(w => w.Message));
        Assert.NotNull(r.Lesson);
    }

    [Fact]
    public void Frontmatter_warnings()
    {
        Assert.Contains("unit: Unit \"P.tour\" does not belong to track C.", WarningsOf(Lesson(M("unit", "P.tour"))));
        Assert.Contains("categry: Unknown field \"categry\". It will be ignored.", WarningsOf(Lesson(M("categry", "x"))));
        Assert.Contains("glossary[2]: The term \"A\" is listed twice.", WarningsOf(Lesson(M("glossary", "[{term: a, definition: b}, {term: A, definition: c}]"))));
        Assert.Contains("questions: The lesson has no questions.", WarningsOf(Lesson(null, [])));
        Assert.Contains("body: There is no \"## Practice\" heading.", WarningsOf(Lesson().Replace("## Practice\n", "")));
    }

    [Theory]
    [InlineData("type: text\nprompt: \"x\"\naccept: [a]\nexplanation: \"x\"", "question at line 19: Missing required field \"id\".")]
    [InlineData("id: \"q 1\"\ntype: text\nprompt: \"x\"\naccept: [a]\nexplanation: \"x\"", "question at line 19: \"q 1\" is not a valid question id.")]
    [InlineData("id: q1\nprompt: \"x\"\nexplanation: \"x\"", "q1.type: Missing required field \"type\".")]
    [InlineData("id: q1\ntype: essay\nprompt: \"x\"\nexplanation: \"x\"", "q1.type: \"essay\" is not a question type.")]
    [InlineData("id: q1\ntype: text\naccept: [a]\nexplanation: \"x\"", "q1.prompt: Missing required field \"prompt\".")]
    [InlineData("id: q1\ntype: text\nprompt: \"x\"\naccept: [a]", "q1.explanation: Missing required field \"explanation\".")]
    [InlineData("id: q1\ntype: text\nprompt: [x]\naccept: [a]\nexplanation: \"x\"", "q1.prompt: prompt must be text.")]
    [InlineData("not a mapping", "question at line 19: A question block must contain \"field: value\" lines.")]
    public void Question_errors_for_all_types(string block, string expected) => Assert.Contains(expected, ErrorsOf(Lesson(null, [block])));

    [Theory]
    [InlineData("accept: [a]\nhints: \"one\"", "q1.hints: hints must be a list of text items.")]
    [InlineData("accept: [a]\nhints: [a, b, c, d]", "q1.hints: There are 4 hints. The maximum is 3.")]
    [InlineData("accept: [a]\ntags: \"x\"", "q1.tags: tags must be a list of text items.")]
    public void Hint_and_tag_errors(string fields, string expected) => Assert.Contains(expected, ErrorsOf(Lesson(null, [Q("text", fields)])));

    [Fact]
    public void Duplicate_question_ids()
    {
        Assert.Contains("q1.id: The question id \"q1\" is already used at line 20.", ErrorsOf(Lesson(null, [Q("text", "accept: [a]"), Q("text", "accept: [b]")])));
    }

    [Fact]
    public void Yaml_error_inside_a_block()
    {
        Assert.Contains(ErrorsOf(Lesson(null, ["id: q1\ntype: text\nprompt: \"x\nexplanation: y"])), e => e.StartsWith("question at line 19: YAML syntax error"));
    }

    [Theory]
    [InlineData("mcq", "answer: A", "q1.choices: Missing required field \"choices\" for a mcq question.")]
    [InlineData("mcq", "choices: \"a, b\"\nanswer: A", "q1.choices: choices must be a list.")]
    [InlineData("mcq", "choices: [a]\nanswer: A", "q1.choices: There are 1 choices. Use 2 to 6.")]
    [InlineData("mcq", "choices: [a, b, c, d, e, f, g]\nanswer: A", "q1.choices: There are 7 choices. Use 2 to 6.")]
    [InlineData("mcq", "choices:\n  - Title: options\n  - b\nanswer: A", "q1.choices[1]: Item 1 of choices is not plain text.")]
    [InlineData("mcq", "choices: [a, b, c]", "q1.answer: Missing required field \"answer\" for a mcq question.")]
    [InlineData("mcq", "choices: [a, b, c]\nanswer: AB", "q1.answer: \"AB\" must be a single letter.")]
    [InlineData("mcq", "choices: [a, b, c]\nanswer: E", "q1.answer: \"E\" is not one of the choices A–C.")]
    [InlineData("mcq", "choices: [a, b]\nanswer: A\nfeedback: \"x\"", "q1.feedback: feedback must map choice letters to text.")]
    [InlineData("mcq", "choices: [a, b]\nanswer: A\nfeedback: {D: \"x\"}", "q1.feedback.D: \"D\" is not one of the choices A–B.")]
    [InlineData("mcq", "choices: [a, b]\nanswer: A\nfeedback: {B: [x]}", "q1.feedback.B: Feedback for B must be text.")]
    [InlineData("multi", "choices: [a, b, c]\nanswer: A", "q1.answer: answer must be a list of letters.")]
    [InlineData("multi", "choices: [a, b, c]\nanswer: []", "q1.answer: answer must list at least one letter.")]
    [InlineData("multi", "choices: [a, b, c]\nanswer: [A, F]", "q1.answer: \"F\" is not one of the choices A–C.")]
    [InlineData("multi", "choices: [a, b, c]\nanswer: [A, A]", "q1.answer: \"A\" is listed twice.")]
    [InlineData("multi", "choices: [a, b, c]\nanswer: [A, 3]", "q1.answer: 3 must be a single letter.")]
    [InlineData("numeric", "tolerance: 1", "q1.answer: Missing required field \"answer\" for a numeric question.")]
    [InlineData("numeric", "answer: \"five\"", "q1.answer: \"five\" must be a number.")]
    [InlineData("numeric", "answer: 5\ntolerance: -1", "q1.tolerance: -1 must be a number of 0 or more.")]
    [InlineData("numeric", "answer: 5\nunit: [px]", "q1.unit: unit must be text.")]
    [InlineData("text", "case_sensitive: false", "q1.accept: Missing required field \"accept\" for a text question.")]
    [InlineData("text", "accept: []", "q1.accept: There are 0 accept. Use 1 to 50.")]
    [InlineData("text", "accept: [a]\ncase_sensitive: \"yes\"", "q1.case_sensitive: case_sensitive must be true or false.")]
    [InlineData("fill", "case_sensitive: true", "q1.blanks: Missing required field \"blanks\" for a fill question.")]
    [InlineData("fill", "blanks: \"emit\"", "q1.blanks: blanks must be a list of lists.")]
    [InlineData("fill", "blanks:\n  - []", "q1.blanks[1]: Blank 1 must list at least one accepted answer as text.")]
    [InlineData("fill", "blanks:\n  - {a: 1}", "q1.blanks[1]: Blank 1 must be a list of accepted answers.")]
    [InlineData("fill", "blanks:\n  - [emit]", "q1.prompt: The prompt has no blanks.")]
    [InlineData("order", "items: [a, b]", "q1.items: There are 2 items. Use 3 to 7.")]
    [InlineData("order", "items: [a, b, a]", "q1.items: Two items have the same text, so the order is ambiguous.")]
    [InlineData("match", "pairs: \"x\"", "q1.pairs: pairs must be a list of [left, right] pairs.")]
    [InlineData("match", "pairs: [[a, 1], [b, 2]]", "q1.pairs: There are 2 pairs. Use 3 to 6.")]
    [InlineData("match", "pairs: [[a, 1], [b, 2], [c]]", "q1.pairs[3]: Pair 3 must be exactly two text items.")]
    [InlineData("match", "pairs: [[a, 1], [a, 2], [c, 3]]", "q1.pairs[2]: The left item \"a\" is used twice.")]
    [InlineData("match", "pairs: [[a, 1], [b, 1], [c, 3]]", "q1.pairs[2]: The right item \"1\" is used twice.")]
    [InlineData("reflect", "rubric: [a]", "q1.model_answer: Missing required field \"model_answer\" for a reflect question.")]
    [InlineData("reflect", "model_answer: [a]", "q1.model_answer: model_answer must be text.")]
    [InlineData("reflect", "model_answer: \"x\"\nrubric: \"a\"", "q1.rubric: rubric must be a list of text items.")]
    [InlineData("task", "done_means: \"x\"", "q1.instructions: Missing required field \"instructions\" for a task question.")]
    [InlineData("task", "instructions: \"x\"", "q1.done_means: Missing required field \"done_means\" for a task question.")]
    [InlineData("task", "instructions: [x]\ndone_means: \"x\"", "q1.instructions: instructions must be text.")]
    public void Per_type_errors(string type, string fields, string expected) => Assert.Contains(expected, ErrorsOf(Lesson(null, [Q(type, fields)])));

    [Fact]
    public void Fill_blank_count_must_match()
    {
        Assert.Contains("q1.blanks: The prompt has 2 blanks (___) but blanks lists 1.", ErrorsOf(Lesson(null, ["id: q1\ntype: fill\nprompt: \"a ___ b ___\"\nexplanation: \"x\"\nblanks:\n  - [x]"])));
    }

    [Fact]
    public void Per_type_warnings()
    {
        Assert.Contains("q1.choices: \"choices\" is not a field of text questions. It will be ignored.", WarningsOf(Lesson(null, [Q("text", "accept: [a]\nchoices: [x]")])));
        Assert.Contains("q1.feedback.A: A is the correct answer, so this feedback is never shown.", WarningsOf(Lesson(null, [Q("mcq", "choices: [a, b]\nanswer: A\nfeedback: {A: \"x\"}")])));
        var r = Validator.Load(Lesson(null, ["id: q1\ntype: fill\nprompt: \"a ___\"\nexplanation: \"x\"\nblanks:\n  - emit"]));
        Assert.Empty(r.Errors);
        Assert.Contains("Blank 1 should be a list.", r.Warnings.Select(w => w.Message));
        Assert.Equal(["emit"], Assert.IsType<FillQuestion>(r.Lesson!.Questions[0]).Blanks[0]);
    }

    public static TheoryData<int, string, string> BrokenCases => new()
    {
        { 1, "summary", "Missing required field \"summary\"" },
        { 2, "id", "\"Broken Lesson\" is not a valid id" },
        { 4, "track", "\"Design\" must be one of" },
        { 6, "tier", "7 must be a whole number from 0 to 4" },
        { 9, "paper_type", "\"essay\" must be one of" },
        { 11, "version", "version 2 is not supported" },
        { 26, "q1.answer", "\"E\" is not one of the choices A–C" },
        { 33, "q1.id", "already used at line 19" },
        { 34, "q1.type", "\"essay\" is not a question type" },
        { 43, "q3.blanks", "2 blanks (___) but blanks lists 1" },
        { 46, "q3.hints", "4 hints. The maximum is 3" },
        { 57, "q4.answer", "\"five\" must be a number" },
        { 65, "q5.items", "2 items. Use 3 to 7" },
        { 75, "question at line 71", "YAML syntax error" },
        { 79, "question", "never closed" },
    };

    [Theory]
    [MemberData(nameof(BrokenCases))]
    public void Broken_fixture_reports_each_error_at_its_line(int line, string field, string message)
    {
        var r = Validator.Load(Repo.Fixture("broken.lesson.md"), Units);
        Assert.True(r.Errors.Any(e => e.Line == line && e.Field == field && e.Message.Contains(message)),
            $"expected an error at line {line} on {field}. Got:\n{string.Join('\n', r.Errors.Select(Validator.FormatIssue))}");
    }

    [Fact]
    public void Broken_fixture_blocks_import_and_builds_a_report()
    {
        var r = Validator.Load(Repo.Fixture("broken.lesson.md"), Units);
        Assert.Null(r.Lesson);
        Assert.True(r.Errors.Count >= 8);
        var report = Validator.ErrorReport(r.Errors);
        Assert.Equal("Fix these errors in the lesson file and output the full corrected lesson:", report.Split('\n')[0]);
        Assert.Contains("line 26, q1.answer: \"E\" is not one of the choices A–C.", report);
        Assert.Contains("line 4, track: \"Design\" must be one of C, X, P, D, A, S, N (or K for capstones).", report);
    }
}
