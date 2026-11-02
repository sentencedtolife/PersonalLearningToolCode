using Nordlys.Core.Lesson;

namespace Nordlys.Tests;

public class ParseTests
{
    internal const string Mini = """
        ---
        id: t-mini-01
        title: Mini
        track: C
        unit: C.loops
        tier: 1
        energy: 1
        minutes: 5
        paper_type: concept
        done_means: "Done."
        summary: "A tiny lesson."
        version: 1
        ---

        # Mini

        Intro paragraph.

        ~~~question
        id: q1
        type: text
        prompt: "Say hi."
        accept: ["hi"]
        explanation: "Because."
        ~~~

        ## Practice

        ~~~question
        id: q2
        type: text
        prompt: "Say bye."
        accept: ["bye"]
        explanation: "Because."
        ~~~

        """;

    private static readonly string MiniLf = Mini.Replace("\r\n", "\n");

    [Fact]
    public void Frontmatter_fields_and_their_lines()
    {
        var p = Parser.Parse(MiniLf);
        Assert.Empty(p.Issues);
        Assert.Equal("t-mini-01", p.Meta!["id"]);
        Assert.Equal(1L, p.Meta["tier"]);
        Assert.Equal(2, p.MetaLines["id"]);
        Assert.Equal(11, p.MetaLines["summary"]);
    }

    [Fact]
    public void Missing_opening_dashes()
    {
        var p = Parser.Parse("id: x\n");
        Assert.Equal((1, "frontmatter"), (p.Issues[0].Line, p.Issues[0].Field));
    }

    [Fact]
    public void Frontmatter_never_closed()
    {
        Assert.Contains("never closed", Parser.Parse("---\nid: x\ntitle: y\n").Issues[0].Message);
    }

    [Fact]
    public void Yaml_syntax_error_has_a_line_inside_the_frontmatter()
    {
        var p = Parser.Parse("---\nid: x\ntitle: \"unterminated\nsummary: y\n---\n");
        var issue = Assert.Single(p.Issues);
        Assert.StartsWith("YAML syntax error", issue.Message);
        Assert.InRange(issue.Line, 3, 5);
    }

    [Fact]
    public void Splits_blocks_with_lines_practice_flag_and_placeholders()
    {
        var p = Parser.Parse(MiniLf);
        Assert.Equal(2, p.Blocks.Count);
        Assert.Equal((19, false, "<!--Q:q1-->"), (p.Blocks[0].Line, p.Blocks[0].InPractice, p.Blocks[0].Placeholder));
        Assert.Equal((29, true, "<!--Q:q2-->"), (p.Blocks[1].Line, p.Blocks[1].InPractice, p.Blocks[1].Placeholder));
        Assert.Equal(new Dictionary<string, int> { ["id"] = 20, ["type"] = 21, ["prompt"] = 22, ["accept"] = 23, ["explanation"] = 24 }, p.Blocks[0].FieldLines);
        Assert.True(p.HasPractice);
        Assert.Contains("<!--Q:q1-->", p.Body);
        Assert.DoesNotContain("~~~", p.Body);
        Assert.DoesNotContain("Say hi", p.Body);
    }

    [Fact]
    public void Gdscript_backtick_fences_survive_inside_tilde_blocks()
    {
        var r = Validator.Load(Repo.Read("content/lessons/p-godot-tour-01.lesson.md"));
        Assert.Empty(r.Errors);
        var q2 = r.Lesson!.Questions.Single(q => q.Id == "q2");
        Assert.Contains("```gdscript", q2.Prompt);
        Assert.Contains("print(\"Hello from \", name)", q2.Prompt);
        Assert.Contains("```gdscript\nextends Node2D", r.Lesson.Body);
    }

    [Fact]
    public void Block_never_closed()
    {
        var last = Parser.Parse(MiniLf + "\n~~~question\nid: q3\n").Issues[^1];
        Assert.Contains("never closed", last.Message);
        Assert.Equal(37, last.Line);
    }

    [Fact]
    public void Bom_and_windows_line_endings()
    {
        var r = Validator.Load("﻿" + MiniLf.Replace("\n", "\r\n"));
        Assert.Empty(r.Errors);
        Assert.Equal("t-mini-01", r.Lesson!.Meta.Id);
        Assert.Equal("a\nb\nc", Parser.NormalizeText("a\r\nb\rc"));
    }

    [Fact]
    public void Strips_outer_fence_and_chat_text()
    {
        var pasted = $"Here is your lesson!\n\n````lesson\n{MiniLf}````\n\nLet me know if you want changes.";
        Assert.Equal(MiniLf.Trim(), Parser.StripOuterFence(pasted).Trim());
        var r = Validator.Load(pasted);
        Assert.Empty(r.Errors);
        Assert.Equal(2, r.Lesson!.Questions.Count);
    }

    [Fact]
    public void Five_backticks_without_info_string()
    {
        Assert.Equal(MiniLf.Trim(), Parser.StripOuterFence($"chat\n`````\n{MiniLf}`````\nmore chat").Trim());
    }

    [Fact]
    public void Text_without_fence_is_unchanged() => Assert.Equal(MiniLf, Parser.StripOuterFence(MiniLf));

    [Fact]
    public void ReplaceLessonId_changes_only_the_frontmatter_id()
    {
        var o = Parser.ReplaceLessonId(MiniLf, "t-mini-01-2");
        Assert.Equal("id: t-mini-01-2", o.Split('\n')[1]);
        Assert.Equal(3, o.Split('\n').Count(l => l.StartsWith("id: ")));
        Assert.Equal("t-mini-01-2", Validator.Load(o).Lesson!.Meta.Id);
    }

    [Fact]
    public void NextFreeId_appends_numbers()
    {
        var taken = new HashSet<string> { "a", "a-2" };
        Assert.Equal("a-3", Parser.NextFreeId("a", taken.Contains));
        Assert.Equal("b-2", Parser.NextFreeId("b", taken.Contains));
    }
}
