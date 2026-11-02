// Lesson file text -> raw frontmatter + question blocks, with line numbers.
// Question blocks are found by splitting lines, not by a Markdown parser (HANDOFF 5.1).
using System.Text.RegularExpressions;

namespace Nordlys.Core.Lesson;

public sealed class RawBlock
{
    /// <summary>Parsed YAML, or null if it failed to parse (see <see cref="Failed"/>).</summary>
    public object? Data { get; init; }
    public bool Failed { get; init; }
    /// <summary>1-based line of the opening ~~~question fence.</summary>
    public int Line { get; init; }
    /// <summary>Top-level YAML key -> 1-based line in the file.</summary>
    public required Dictionary<string, int> FieldLines { get; init; }
    public bool InPractice { get; init; }
    public required string Placeholder { get; init; }
}

public sealed class ParsedLesson
{
    public Dictionary<string, object?>? Meta { get; init; }
    public required Dictionary<string, int> MetaLines { get; init; }
    public required string Body { get; init; }
    public required List<RawBlock> Blocks { get; init; }
    public bool HasPractice { get; init; }
    /// <summary>Structural problems: missing frontmatter, YAML syntax errors, unclosed blocks.</summary>
    public required List<Issue> Issues { get; init; }
}

public static partial class Parser
{
    public static readonly Regex PracticeHeading = PracticeRe();
    public static readonly Regex Placeholder = PlaceholderRe();

    /// <summary>Removes a byte-order mark and converts Windows/old-Mac line endings to \n.</summary>
    public static string NormalizeText(string text) => text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>
    /// If the text contains an outer fence (a line of 4+ backticks, optionally followed by "lesson", and a later
    /// line of the same number of backticks), keep only what is between them. Everything outside is chat text.
    /// </summary>
    public static string StripOuterFence(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var open = OpenFenceRe().Match(lines[i]);
            if (!open.Success) continue;
            var fence = open.Groups[1].Value;
            for (var j = lines.Length - 1; j > i; j--)
                if (lines[j].Trim() == fence) return string.Join('\n', lines[(i + 1)..j]);
            return text;
        }
        return text;
    }

    /// <summary>Changes the frontmatter id: line (used for "Keep both" on import). The text must already be normalised.</summary>
    public static string ReplaceLessonId(string text, string newId)
    {
        var lines = text.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---") return text;
        for (var i = 1; i < lines.Length && lines[i].Trim() != "---"; i++)
        {
            if (IdLineRe().IsMatch(lines[i]))
            {
                lines[i] = $"id: {newId}";
                return string.Join('\n', lines);
            }
        }
        return text;
    }

    /// <summary>First free id of the form &lt;id&gt;-2, &lt;id&gt;-3, …</summary>
    public static string NextFreeId(string id, Func<string, bool> taken)
    {
        for (var n = 2; ; n++)
            if (!taken($"{id}-{n}")) return $"{id}-{n}";
    }

    private static Issue YamlIssue(YamlError e, int firstLine, string field) => new(
        Severity.Error, firstLine + e.Line, field,
        $"YAML syntax error: {e.Reason.Replace('\n', ' ')}.",
        "Check indentation (2 spaces, no tabs) and put text that contains \": \" or starts with a special character (` [ { * & ! | > ' \" % @) in double quotes.");

    private static Dictionary<string, int> KeyLines(string[] lines, int from, int to)
    {
        var o = new Dictionary<string, int>();
        for (var i = from; i < to; i++)
        {
            var m = KeyLineRe().Match(lines[i]);
            if (m.Success) o.TryAdd(m.Groups[1].Value, i + 1);
        }
        return o;
    }

    public static ParsedLesson Parse(string text)
    {
        var lines = text.Split('\n');
        var issues = new List<Issue>();
        Dictionary<string, object?>? meta = null;
        var metaLines = new Dictionary<string, int>();
        var bodyStart = 0;

        if (lines[0].Trim() != "---")
        {
            issues.Add(new(Severity.Error, 1, "frontmatter", "The file must start with a line containing only ---.",
                "Put the frontmatter (id, title, track, …) between two --- lines at the very top of the lesson."));
        }
        else
        {
            var close = -1;
            for (var i = 1; i < lines.Length; i++) if (lines[i].Trim() == "---") { close = i; break; }
            if (close == -1)
            {
                issues.Add(new(Severity.Error, 1, "frontmatter", "The frontmatter is never closed.",
                    "Add a line containing only --- after the last frontmatter field."));
            }
            else
            {
                metaLines = KeyLines(lines, 1, close);
                try
                {
                    var data = Yaml.Load(string.Join('\n', lines[1..close]));
                    if (data is Dictionary<string, object?> d) meta = d;
                    else issues.Add(new(Severity.Error, 2, "frontmatter", "The frontmatter must be a list of \"field: value\" lines.",
                        "Write each field on its own line, e.g. title: \"Core Loops\"."));
                }
                catch (YamlError e)
                {
                    issues.Add(YamlIssue(e, 2, "frontmatter"));
                }
                bodyStart = close + 1;
            }
        }

        var body = new List<string>();
        var blocks = new List<RawBlock>();
        var inPractice = false;
        var hasPractice = false;
        for (var i = bodyStart; i < lines.Length; i++)
        {
            var line = lines[i];
            if (PracticeHeading.IsMatch(line)) { inPractice = true; hasPractice = true; }
            if (line.TrimEnd() != "~~~question") { body.Add(line); continue; }

            var close = -1;
            for (var k = i + 1; k < lines.Length; k++) if (lines[k].TrimEnd() == "~~~") { close = k; break; }
            if (close == -1)
            {
                issues.Add(new(Severity.Error, i + 1, "question", "This question block is never closed.",
                    "Add a line containing only ~~~ after the last field of the question."));
                break;
            }
            object? data = null;
            var failed = false;
            try
            {
                data = Yaml.Load(string.Join('\n', lines[(i + 1)..close]));
            }
            catch (YamlError e)
            {
                failed = true;
                issues.Add(YamlIssue(e, i + 2, $"question at line {i + 1}"));
            }
            var rawId = data is Dictionary<string, object?> m && m.TryGetValue("id", out var idv) && idv is string or long or double ? Yaml.Str(idv) : $"#{blocks.Count}";
            var placeholder = $"<!--Q:{rawId}-->";
            blocks.Add(new RawBlock { Data = data, Failed = failed, Line = i + 1, FieldLines = KeyLines(lines, i + 1, close), InPractice = inPractice, Placeholder = placeholder });
            body.Add("");
            body.Add(placeholder);
            body.Add("");
            i = close;
        }

        return new ParsedLesson { Meta = meta, MetaLines = metaLines, Body = string.Join('\n', body), Blocks = blocks, HasPractice = hasPractice, Issues = issues };
    }

    [GeneratedRegex(@"^##\s+Practice\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PracticeRe();

    [GeneratedRegex(@"^<!--Q:(.*)-->$")]
    private static partial Regex PlaceholderRe();

    [GeneratedRegex(@"^(`{4,})\s*(?:lesson)?\s*$")]
    private static partial Regex OpenFenceRe();

    [GeneratedRegex(@"^([A-Za-z_][\w-]*)\s*:")]
    private static partial Regex KeyLineRe();

    [GeneratedRegex(@"^id\s*:")]
    private static partial Regex IdLineRe();
}
