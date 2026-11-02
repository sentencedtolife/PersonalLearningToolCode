// ParsedLesson -> errors and warnings (with line numbers and suggested fixes) -> typed LessonDoc.
// Errors block import. Warnings don't. Messages are part of the contract: Claude reads them in the error report.
using System.Text.RegularExpressions;
using static Nordlys.Core.Lesson.Yaml;

namespace Nordlys.Core.Lesson;

public sealed record ValidationResult(IReadOnlyList<Issue> Errors, IReadOnlyList<Issue> Warnings, LessonDoc? Lesson, string Text);

public static partial class Validator
{
    public static readonly Regex IdPattern = IdRe();
    public static readonly Regex BlankPattern = BlankRe();

    private static readonly string[] MetaRequired = ["id", "title", "track", "unit", "tier", "energy", "minutes", "paper_type", "done_means", "summary", "version"];
    private static readonly HashSet<string> MetaKnown = [.. MetaRequired, "prerequisites", "tags", "glossary", "seed"];
    private static readonly Dictionary<string, string> MetaExample = new()
    {
        ["id"] = "id: c-core-loops-01",
        ["title"] = "title: \"Core Loops\"",
        ["track"] = "track: C",
        ["unit"] = "unit: C.loops",
        ["tier"] = "tier: 1",
        ["energy"] = "energy: 2",
        ["minutes"] = "minutes: 25",
        ["paper_type"] = "paper_type: concept",
        ["done_means"] = "done_means: \"You can …\"",
        ["summary"] = "summary: \"One sentence about the lesson.\"",
        ["version"] = "version: 1",
    };

    private static readonly string[] QCommon = ["id", "type", "prompt", "hints", "explanation", "tags"];
    private static readonly Dictionary<string, string[]> QFields = new()
    {
        ["mcq"] = ["choices", "answer", "feedback"],
        ["multi"] = ["choices", "answer"],
        ["numeric"] = ["answer", "tolerance", "unit"],
        ["text"] = ["accept", "case_sensitive"],
        ["fill"] = ["blanks", "case_sensitive"],
        ["order"] = ["items"],
        ["match"] = ["pairs"],
        ["reflect"] = ["model_answer", "rubric"],
        ["task"] = ["instructions", "done_means"],
    };

    private const string QuoteFix = "Put the text in double quotes. A colon followed by a space makes YAML read it as \"key: value\".";

    private sealed class Collector
    {
        public List<Issue> Errors { get; } = [];
        public List<Issue> Warnings { get; } = [];
        public void Error(int line, string field, string message, string fix) => Errors.Add(new(Severity.Error, line, field, message, fix));
        public void Warn(int line, string field, string message, string fix) => Warnings.Add(new(Severity.Warning, line, field, message, fix));
    }

    private static bool Missing(object? v) => v is null || v is "";
    private static string LetterRange(int n) => n <= 1 ? "A" : $"A–{Format.Letter(n - 1)}";

    /// <summary>Normalises text, removes an outer fence, parses and validates.</summary>
    public static ValidationResult Load(string text, ISet<string>? knownUnits = null)
    {
        var clean = Parser.StripOuterFence(Parser.NormalizeText(text));
        return Validate(Parser.Parse(clean), knownUnits) with { Text = clean };
    }

    public static ValidationResult Validate(ParsedLesson parsed, ISet<string>? knownUnits = null)
    {
        var c = new Collector();
        c.Errors.AddRange(parsed.Issues);
        if (parsed.Meta is { } meta) ValidateMeta(meta, parsed.MetaLines, c, knownUnits);

        var seen = new Dictionary<string, int>();
        for (var i = 0; i < parsed.Blocks.Count; i++) ValidateBlock(parsed.Blocks[i], i, seen, c);

        if (parsed.Blocks.Count == 0)
            c.Warn(0, "questions", "The lesson has no questions.", "Add question blocks (~~~question … ~~~), and put most of them under \"## Practice\".");
        else if (!parsed.HasPractice)
            c.Warn(0, "body", "There is no \"## Practice\" heading.", "Add a line \"## Practice\" before the practice questions.");

        var lesson = c.Errors.Count == 0 && parsed.Meta != null ? Build(parsed) : null;
        return new ValidationResult(c.Errors.OrderBy(e => e.Line).ToList(), c.Warnings.OrderBy(w => w.Line).ToList(), lesson, "");
    }

    private static void ValidateMeta(Dictionary<string, object?> meta, Dictionary<string, int> lines, Collector c, ISet<string>? knownUnits)
    {
        int At(string k) => lines.TryGetValue(k, out var l) ? l : 1;
        object? Get(string k) => meta.TryGetValue(k, out var v) ? v : null;

        foreach (var k in MetaRequired)
            if (Missing(Get(k))) c.Error(1, k, $"Missing required field \"{k}\".", $"Add a frontmatter line like: {MetaExample[k]}");
        foreach (var k in meta.Keys)
            if (!MetaKnown.Contains(k)) c.Warn(At(k), k, $"Unknown field \"{k}\". It will be ignored.", "Remove it, or check the spelling.");

        var id = Get("id");
        if (!Missing(id) && !(IsScalar(id) && IdPattern.IsMatch(Str(id))))
            c.Error(At("id"), "id", $"{Show(id)} is not a valid id.", "Use only lowercase letters, digits, \"-\" and \".\", starting with a letter or digit, e.g. p-signals-01.");
        foreach (var k in new[] { "title", "done_means", "summary" })
            if (!Missing(Get(k)) && !IsText(Get(k))) c.Error(At(k), k, $"{k} must be text.", QuoteFix);

        var track = Get("track");
        var trackOk = track is string ts && Format.Tracks.Contains(ts);
        if (!Missing(track) && !trackOk)
            c.Error(At("track"), "track", $"{Show(track)} must be one of C, X, P, D, A, S, N (or K for capstones).", "Use the one-letter track code, e.g. track: C");

        var unit = Get("unit");
        if (!Missing(unit))
        {
            if (unit is not string u)
            {
                c.Error(At("unit"), "unit", "unit must be text, e.g. C.loops.", "Use a unit id from curriculum_units.");
            }
            else
            {
                if (knownUnits != null && !knownUnits.Contains(u))
                    c.Warn(At("unit"), "unit", $"\"{u}\" is not a unit in curriculum.json.", "Use a unit id from curriculum_units, e.g. C.loops.");
                if (trackOk && !u.StartsWith($"{track}."))
                    c.Warn(At("unit"), "unit", $"Unit \"{u}\" does not belong to track {track}.", $"Use a unit that starts with \"{track}.\", or change the track.");
            }
        }
        IntField(meta, "tier", 0, 4, At, c);
        IntField(meta, "energy", 1, 3, At, c);
        IntField(meta, "minutes", 1, 600, At, c);

        var pt = Get("paper_type");
        if (!Missing(pt) && !(pt is string p && Format.PaperTypes.Contains(p)))
            c.Error(At("paper_type"), "paper_type", $"{Show(pt)} must be one of {string.Join(", ", Format.PaperTypes)}.", "e.g. paper_type: concept");

        var version = Get("version");
        if (!Missing(version) && !(IsNumber(version) && ToDouble(version) == Format.Version))
            c.Error(At("version"), "version", $"version {Show(version)} is not supported. This app reads format version {Format.Version}.", $"Set version: {Format.Version}");

        foreach (var k in new[] { "prerequisites", "tags" })
        {
            var v = Get(k);
            if (v is null) continue;
            if (v is not List<object?> list || !list.All(IsScalar))
                c.Error(At(k), k, $"{k} must be a list of text items.", $"e.g. {k}: [{(k == "tags" ? "core-loop" : "C.loops")}]");
        }

        var glossary = Get("glossary");
        if (glossary != null)
        {
            if (glossary is not List<object?> gl)
            {
                c.Error(At("glossary"), "glossary", "glossary must be a list of {term, definition}.", "Write each entry as \"- term: …\" followed by \"  definition: …\".");
            }
            else
            {
                var terms = new HashSet<string>();
                for (var i = 0; i < gl.Count; i++)
                {
                    if (gl[i] is not Dictionary<string, object?> g || !IsText(g.GetValueOrDefault("term")) || !IsText(g.GetValueOrDefault("definition")))
                    {
                        c.Error(At("glossary"), $"glossary[{i + 1}]", $"Glossary entry {i + 1} needs both \"term\" and \"definition\" as text.", "Write it as \"- term: …\" followed by \"  definition: …\".");
                        continue;
                    }
                    var term = (string)g["term"]!;
                    if (!terms.Add(term.Trim().ToLowerInvariant()))
                        c.Warn(At("glossary"), $"glossary[{i + 1}]", $"The term \"{term}\" is listed twice.", "Remove the duplicate entry.");
                }
            }
        }

        var seed = Get("seed");
        if (seed != null)
        {
            if (seed is not Dictionary<string, object?> s)
            {
                c.Error(At("seed"), "seed", "seed must be {theme, verb, constraint}.", "e.g. seed: {theme: lighthouse, verb: push, constraint: \"one button\"}");
            }
            else
            {
                foreach (var (k, v) in s)
                {
                    if (k is not ("theme" or "verb" or "constraint")) c.Warn(At("seed"), $"seed.{k}", $"Unknown seed field \"{k}\".", "Use only theme, verb and constraint.");
                    else if (!IsScalar(v)) c.Error(At("seed"), $"seed.{k}", $"seed.{k} must be text.", QuoteFix);
                }
            }
        }
    }

    private static void IntField(Dictionary<string, object?> meta, string k, int min, int max, Func<string, int> at, Collector c)
    {
        var v = meta.GetValueOrDefault(k);
        if (Missing(v)) return;
        if (!IsNumber(v) || !IsInteger(v) || ToDouble(v) < min || ToDouble(v) > max)
            c.Error(at(k), k, $"{Show(v)} must be a whole number from {min} to {max}.", $"e.g. {k}: {(min == 0 ? 1 : min)}");
    }

    private static void ValidateBlock(RawBlock b, int index, Dictionary<string, int> seen, Collector c)
    {
        if (b.Failed) return; // YAML syntax error, already reported
        var label = $"question at line {b.Line}";
        if (b.Data is not Dictionary<string, object?> q)
        {
            c.Error(b.Line, label, "A question block must contain \"field: value\" lines.", "Start with id, type, prompt and explanation.");
            return;
        }
        int At(string k) => b.FieldLines.TryGetValue(k, out var l) ? l : b.Line;
        object? Get(string k) => q.GetValueOrDefault(k);

        var name = $"q#{index + 1}";
        var idv = Get("id");
        if (Missing(idv))
        {
            c.Error(b.Line, label, "Missing required field \"id\".", $"Add a line like: id: q{index + 1}");
        }
        else if (!IsScalar(idv) || !QidRe().IsMatch(Str(idv)))
        {
            c.Error(At("id"), label, $"{Show(idv)} is not a valid question id.", "Use letters, digits, \"-\" or \"_\", e.g. q3.");
        }
        else
        {
            name = Str(idv);
            if (seen.TryGetValue(name, out var firstLine))
                c.Error(At("id"), $"{name}.id", $"The question id \"{name}\" is already used at line {firstLine}.", "Give every question a different id: q1, q2, q3, …");
            else seen[name] = At("id");
        }

        var type = Get("type");
        var typeOk = type is string ts && Format.QuestionTypes.Contains(ts);
        if (Missing(type))
            c.Error(b.Line, $"{name}.type", "Missing required field \"type\".", $"Add a line like: type: mcq (one of {string.Join(", ", Format.QuestionTypes)}).");
        else if (!typeOk)
            c.Error(At("type"), $"{name}.type", $"{Show(type)} is not a question type.", $"Use one of: {string.Join(", ", Format.QuestionTypes)}.");

        foreach (var k in new[] { "prompt", "explanation" })
        {
            if (Missing(Get(k)))
                c.Error(b.Line, $"{name}.{k}", $"Missing required field \"{k}\".", k == "prompt" ? "Add prompt: \"…\" with the question text." : "Add explanation: \"…\" that says WHY the answer is right.");
            else if (!IsText(Get(k)))
                c.Error(At(k), $"{name}.{k}", $"{k} must be text.", QuoteFix);
        }
        var hints = Get("hints");
        if (hints != null)
        {
            if (hints is not List<object?> hl || !hl.All(IsText))
                c.Error(At("hints"), $"{name}.hints", "hints must be a list of text items.", "Write each hint on its own line starting with \"- \".");
            else if (hl.Count > 3)
                c.Error(At("hints"), $"{name}.hints", $"There are {hl.Count} hints. The maximum is 3.", "Keep the 3 most useful hints: a nudge, a narrowing, a near-answer.");
        }
        var tags = Get("tags");
        if (tags != null && (tags is not List<object?> tl || !tl.All(IsScalar)))
            c.Error(At("tags"), $"{name}.tags", "tags must be a list of text items.", "e.g. tags: [vectors]");
        if (!typeOk) return;

        var t = (string)type!;
        var known = new HashSet<string>([.. QCommon, .. QFields[t]]);
        foreach (var k in q.Keys)
            if (!known.Contains(k)) c.Warn(At(k), $"{name}.{k}", $"\"{k}\" is not a field of {t} questions. It will be ignored.", $"{t} questions use: {string.Join(", ", QFields[t])}.");

        string F(string k) => $"{name}.{k}";
        bool Required(string k, string hint)
        {
            if (Missing(Get(k))) { c.Error(b.Line, F(k), $"Missing required field \"{k}\" for a {t} question.", hint); return false; }
            return true;
        }

        switch (t)
        {
            case "mcq":
            case "multi":
            {
                var n = CheckList(Get("choices"), "choices", 2, 6, At, F, c, Required);
                var answer = Get("answer");
                if (t == "mcq")
                {
                    if (!Required("answer", "Add answer: B (the letter of the correct choice; A is the first choice).")) break;
                    if (answer is not string a || Format.LetterIndex(a) < 0)
                        c.Error(At("answer"), F("answer"), $"{Show(answer)} must be a single letter.", "e.g. answer: B");
                    else if (n is int nn && Format.LetterIndex(a) >= nn)
                        c.Error(At("answer"), F("answer"), $"\"{a}\" is not one of the choices {LetterRange(nn)}.", $"Use a letter from {LetterRange(nn)}. A is the first choice.");
                    var fb = Get("feedback");
                    if (fb != null)
                    {
                        if (fb is not Dictionary<string, object?> fm)
                        {
                            c.Error(At("feedback"), F("feedback"), "feedback must map choice letters to text.", "e.g. feedback:\n  A: \"Why A is wrong.\"");
                        }
                        else
                        {
                            foreach (var (k, v) in fm)
                            {
                                if (Format.LetterIndex(k) < 0 || (n is int m && Format.LetterIndex(k) >= m))
                                    c.Error(At("feedback"), F($"feedback.{k}"), $"\"{k}\" is not one of the choices{(n is int r ? $" {LetterRange(r)}" : "")}.", "Use the letter of a wrong choice.");
                                else if (!IsText(v))
                                    c.Error(At("feedback"), F($"feedback.{k}"), $"Feedback for {k} must be text.", QuoteFix);
                                else if (answer is string ans && string.Equals(k, ans, StringComparison.OrdinalIgnoreCase))
                                    c.Warn(At("feedback"), F($"feedback.{k}"), $"{k} is the correct answer, so this feedback is never shown.", "Feedback is for wrong choices. Put the reasoning in explanation instead.");
                            }
                        }
                    }
                }
                else
                {
                    if (!Required("answer", "Add answer: [A, C] (the letters of all correct choices).")) break;
                    if (answer is not List<object?> al)
                    {
                        c.Error(At("answer"), F("answer"), "answer must be a list of letters.", "e.g. answer: [A, C]");
                    }
                    else if (al.Count == 0)
                    {
                        c.Error(At("answer"), F("answer"), "answer must list at least one letter.", "e.g. answer: [A, C]");
                    }
                    else
                    {
                        var used = new HashSet<string>();
                        foreach (var a in al)
                        {
                            if (a is not string s || Format.LetterIndex(s) < 0)
                                c.Error(At("answer"), F("answer"), $"{Show(a)} must be a single letter.", "e.g. answer: [A, C]");
                            else if (n is int nn && Format.LetterIndex(s) >= nn)
                                c.Error(At("answer"), F("answer"), $"\"{s}\" is not one of the choices {LetterRange(nn)}.", $"Use letters from {LetterRange(nn)}.");
                            else if (used.Contains(s.ToUpperInvariant()))
                                c.Error(At("answer"), F("answer"), $"\"{s}\" is listed twice.", "List each correct letter once.");
                            if (a is string s2) used.Add(s2.ToUpperInvariant());
                        }
                    }
                }
                break;
            }
            case "numeric":
            {
                var a = Get("answer");
                if (Required("answer", "Add answer: 42 (a number, no quotes).") && !(IsNumber(a) && double.IsFinite(ToDouble(a))))
                    c.Error(At("answer"), F("answer"), $"{Show(a)} must be a number.", "Write the number without quotes or units, e.g. answer: 3.5");
                var tol = Get("tolerance");
                if (tol != null && (!IsNumber(tol) || ToDouble(tol) < 0))
                    c.Error(At("tolerance"), F("tolerance"), $"{Show(tol)} must be a number of 0 or more.", "e.g. tolerance: 0.5");
                var unit = Get("unit");
                if (unit != null && !IsScalar(unit)) c.Error(At("unit"), F("unit"), "unit must be text.", "e.g. unit: \"px\"");
                break;
            }
            case "text":
                CheckList(Get("accept"), "accept", 1, 50, At, F, c, Required);
                BoolField(q, "case_sensitive", At, F, c);
                break;
            case "fill":
            {
                BoolField(q, "case_sensitive", At, F, c);
                if (!Required("blanks", "Add blanks: with one list of accepted answers per ___ in the prompt.")) break;
                if (Get("blanks") is not List<object?> blanks) { c.Error(At("blanks"), F("blanks"), "blanks must be a list of lists.", "e.g. blanks:\n  - [\"emit\"]"); break; }
                for (var i = 0; i < blanks.Count; i++)
                {
                    var bl = blanks[i];
                    if (bl is List<object?> list)
                    {
                        if (list.Count == 0 || !list.All(IsScalar)) c.Error(At("blanks"), F($"blanks[{i + 1}]"), $"Blank {i + 1} must list at least one accepted answer as text.", "e.g. - [\"emit\"]");
                    }
                    else if (IsScalar(bl))
                    {
                        c.Warn(At("blanks"), F($"blanks[{i + 1}]"), $"Blank {i + 1} should be a list.", $"Write it as - [{Show(bl)}]");
                    }
                    else
                    {
                        c.Error(At("blanks"), F($"blanks[{i + 1}]"), $"Blank {i + 1} must be a list of accepted answers.", "e.g. - [\"emit\"]");
                    }
                }
                if (Get("prompt") is string prompt)
                {
                    var count = BlankPattern.Matches(prompt).Count;
                    if (count == 0) c.Error(At("prompt"), F("prompt"), "The prompt has no blanks.", "Write ___ (three underscores) where each answer goes.");
                    else if (count != blanks.Count) c.Error(At("blanks"), F("blanks"), $"The prompt has {count} blank{(count == 1 ? "" : "s")} (___) but blanks lists {blanks.Count}.", "Give one list of accepted answers per ___, in order.");
                }
                break;
            }
            case "order":
            {
                var n = CheckList(Get("items"), "items", 3, 7, At, F, c, Required);
                if (n != null)
                {
                    var texts = ((List<object?>)Get("items")!).Select(v => Str(v).Trim()).ToList();
                    if (texts.Distinct().Count() != texts.Count) c.Error(At("items"), F("items"), "Two items have the same text, so the order is ambiguous.", "Make every item different.");
                }
                break;
            }
            case "match":
            {
                if (!Required("pairs", "Add pairs: with 3–6 items like - [\"left\", \"right\"].")) break;
                if (Get("pairs") is not List<object?> pairs) { c.Error(At("pairs"), F("pairs"), "pairs must be a list of [left, right] pairs.", "e.g. - [\"Sprite2D\", \"Shows an image\"]"); break; }
                if (pairs.Count < 3 || pairs.Count > 6) c.Error(At("pairs"), F("pairs"), $"There are {pairs.Count} pairs. Use 3 to 6.", "Add or remove pairs.");
                var lefts = new HashSet<string>();
                var rights = new HashSet<string>();
                for (var i = 0; i < pairs.Count; i++)
                {
                    if (pairs[i] is not List<object?> p || p.Count != 2 || !p.All(IsScalar))
                    {
                        c.Error(At("pairs"), F($"pairs[{i + 1}]"), $"Pair {i + 1} must be exactly two text items.", "e.g. - [\"Sprite2D\", \"Shows an image\"]");
                        continue;
                    }
                    var l = Str(p[0]).Trim();
                    var r = Str(p[1]).Trim();
                    if (!lefts.Add(l)) c.Error(At("pairs"), F($"pairs[{i + 1}]"), $"The left item \"{l}\" is used twice.", "Every left item must be different.");
                    if (!rights.Add(r)) c.Error(At("pairs"), F($"pairs[{i + 1}]"), $"The right item \"{r}\" is used twice.", "Every right item must be different.");
                }
                break;
            }
            case "reflect":
            {
                if (Required("model_answer", "Add model_answer: | followed by an example answer.") && !IsText(Get("model_answer")))
                    c.Error(At("model_answer"), F("model_answer"), "model_answer must be text.", QuoteFix);
                var rubric = Get("rubric");
                if (rubric != null && (rubric is not List<object?> rl || !rl.All(IsText)))
                    c.Error(At("rubric"), F("rubric"), "rubric must be a list of text items.", "Write each point on its own line starting with \"- \".");
                break;
            }
            case "task":
                if (Required("instructions", "Add instructions: | with numbered steps.") && !IsText(Get("instructions")))
                    c.Error(At("instructions"), F("instructions"), "instructions must be text.", QuoteFix);
                if (Required("done_means", "Add done_means: \"…\" with the exact finish condition.") && !IsText(Get("done_means")))
                    c.Error(At("done_means"), F("done_means"), "done_means must be text.", QuoteFix);
                break;
        }
    }

    /// <summary>Checks a list of plain text items. Returns its length if usable, else null.</summary>
    private static int? CheckList(object? v, string k, int min, int max, Func<string, int> at, Func<string, string> f, Collector c, Func<string, string, bool> required)
    {
        if (!required(k, $"Add {k}: with {min}–{max} items, each on its own line starting with \"- \".")) return null;
        if (v is not List<object?> list) { c.Error(at(k), f(k), $"{k} must be a list.", "Write each item on its own line starting with \"- \"."); return null; }
        var ok = true;
        for (var i = 0; i < list.Count; i++)
            if (!IsScalar(list[i])) { c.Error(at(k), f($"{k}[{i + 1}]"), $"Item {i + 1} of {k} is not plain text.", QuoteFix); ok = false; }
        if (list.Count < min || list.Count > max)
        {
            c.Error(at(k), f(k), $"There are {list.Count} {k}. Use {min} to {max}.", $"Add or remove {k}.");
            ok = false;
        }
        return ok || list.Count > 0 ? list.Count : null;
    }

    private static void BoolField(Dictionary<string, object?> q, string k, Func<string, int> at, Func<string, string> f, Collector c)
    {
        var v = q.GetValueOrDefault(k);
        if (v != null && v is not bool) c.Error(at(k), f(k), $"{k} must be true or false.", $"e.g. {k}: true");
    }

    private static List<string> Strs(object? v) => v is List<object?> l ? l.Where(IsScalar).Select(Str).ToList() : [];

    /// <summary>Only called when there are no errors, so fields are known to be well-formed.</summary>
    private static LessonDoc Build(ParsedLesson parsed)
    {
        var m = parsed.Meta!;
        Seed? seed = null;
        if (m.GetValueOrDefault("seed") is Dictionary<string, object?> s)
        {
            string? S(string k) => s.GetValueOrDefault(k) is { } v && Str(v) != "" ? Str(v) : null;
            seed = new Seed(S("theme"), S("verb"), S("constraint"));
        }
        var meta = new LessonMeta
        {
            Id = Str(m["id"]),
            Title = Str(m["title"]),
            Track = Str(m["track"]),
            Unit = Str(m["unit"]),
            Tier = (int)ToDouble(m["tier"]),
            Energy = (int)ToDouble(m["energy"]),
            Minutes = (int)ToDouble(m["minutes"]),
            PaperType = Str(m["paper_type"]),
            DoneMeans = Str(m["done_means"]),
            Summary = Str(m["summary"]),
            Prerequisites = Strs(m.GetValueOrDefault("prerequisites")),
            Tags = Strs(m.GetValueOrDefault("tags")),
            Glossary = m.GetValueOrDefault("glossary") is List<object?> gl
                ? gl.Cast<Dictionary<string, object?>>().Select(g => new GlossaryEntry(Str(g["term"]).Trim(), Str(g["definition"]).Trim())).ToList()
                : [],
            Seed = seed,
            Version = (int)ToDouble(m["version"]),
        };

        var questions = parsed.Blocks.Select(b =>
        {
            var q = (Dictionary<string, object?>)b.Data!;
            var id = Str(q["id"]);
            var prompt = Str(q["prompt"]);
            var explanation = Str(q["explanation"]);
            var hints = Strs(q.GetValueOrDefault("hints"));
            var tags = Strs(q.GetValueOrDefault("tags"));
            Question question = Str(q["type"]) switch
            {
                "mcq" => new McqQuestion
                {
                    Id = id, Prompt = prompt, Explanation = explanation, Choices = Strs(q["choices"]), Answer = Str(q["answer"]).ToUpperInvariant(),
                    Feedback = q.GetValueOrDefault("feedback") is Dictionary<string, object?> fm ? fm.ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => Str(kv.Value)) : new Dictionary<string, string>(),
                },
                "multi" => new MultiQuestion { Id = id, Prompt = prompt, Explanation = explanation, Choices = Strs(q["choices"]), Answer = Strs(q["answer"]).Select(a => a.ToUpperInvariant()).ToList() },
                "numeric" => new NumericQuestion
                {
                    Id = id, Prompt = prompt, Explanation = explanation, Answer = ToDouble(q["answer"]),
                    Tolerance = IsNumber(q.GetValueOrDefault("tolerance")) ? ToDouble(q["tolerance"]) : 0,
                    Unit = IsScalar(q.GetValueOrDefault("unit")) ? Str(q["unit"]) : null,
                },
                "text" => new TextQuestion { Id = id, Prompt = prompt, Explanation = explanation, Accept = Strs(q["accept"]), CaseSensitive = q.GetValueOrDefault("case_sensitive") is true },
                "fill" => new FillQuestion
                {
                    Id = id, Prompt = prompt, Explanation = explanation, CaseSensitive = q.GetValueOrDefault("case_sensitive") is true,
                    Blanks = ((List<object?>)q["blanks"]!).Select(bl => (IReadOnlyList<string>)(bl is List<object?> ? Strs(bl) : [Str(bl)])).ToList(),
                },
                "order" => new OrderQuestion { Id = id, Prompt = prompt, Explanation = explanation, Items = Strs(q["items"]) },
                "match" => new MatchQuestion { Id = id, Prompt = prompt, Explanation = explanation, Pairs = ((List<object?>)q["pairs"]!).Cast<List<object?>>().Select(p => (Str(p[0]), Str(p[1]))).ToList() },
                "reflect" => new ReflectQuestion { Id = id, Prompt = prompt, Explanation = explanation, ModelAnswer = Str(q["model_answer"]), Rubric = Strs(q.GetValueOrDefault("rubric")) },
                _ => new TaskQuestion { Id = id, Prompt = prompt, Explanation = explanation, Instructions = Str(q["instructions"]), TaskDoneMeans = Str(q["done_means"]) },
            };
            return question with { Hints = hints, Tags = tags, InPractice = b.InPractice, Line = b.Line };
        }).ToList();

        return new LessonDoc(meta, parsed.Body, questions);
    }

    /// <summary>"line 14", or "whole lesson" for issues that are not about one line.</summary>
    public static string Where(Issue i) => i.Line > 0 ? $"line {i.Line}" : "whole lesson";

    public static string FormatIssue(Issue i) => $"{Where(i)}, {i.Field}: {i.Message}{(i.Fix != "" ? $" Fix: {i.Fix.Replace('\n', ' ')}" : "")}";

    public static string ErrorReport(IEnumerable<Issue> errors) =>
        string.Join('\n', new[] { "Fix these errors in the lesson file and output the full corrected lesson:" }.Concat(errors.Select(FormatIssue)));

    [GeneratedRegex(@"^[a-z0-9][a-z0-9.-]*$")]
    private static partial Regex IdRe();

    [GeneratedRegex(@"_{3,}")]
    private static partial Regex BlankRe();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex QidRe();
}
