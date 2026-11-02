// The bundled content must follow the same rules Claude is given in generator-prompt.md.
using System.Text.RegularExpressions;
using Nordlys.Core;
using Nordlys.Core.Lesson;

namespace Nordlys.Tests;

public class ContentTests
{
    private static readonly HashSet<string> Units = Repo.KnownUnits();
    public static TheoryData<string> Files => [.. Repo.BundledLessonFiles()];
    private static ValidationResult Load(string file) => Validator.Load(Repo.Read($"content/lessons/{file}"), Units);

    private static readonly Dictionary<int, (int Min, int Max, int QMin, int QMax)> Sizing = new()
    {
        [1] = (250, 450, 3, 4), [2] = (700, 1200, 6, 8), [3] = (1500, 2500, 9, 12),
    };
    private static readonly Regex Banned = new(@"\b(simply|just|obviously|easy|explore|play around|get familiar with)\b|\betc\.", RegexOptions.IgnoreCase);

    private static int PaperWords(string body) =>
        Regex.Replace(body, "<!--Q:[^>]*-->", " ").Split((char[])[' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Count(w => w.Any(char.IsLetterOrDigit));

    [Fact]
    public void Are_the_three_from_the_handoff() =>
        Assert.Equal(["c-core-loops-01.lesson.md", "p-godot-tour-01.lesson.md", "x-next-action-01.lesson.md"], Repo.BundledLessonFiles());

    [Theory]
    [MemberData(nameof(Files))]
    public void Have_no_errors_or_warnings(string file)
    {
        var r = Load(file);
        Assert.Empty(r.Errors);
        Assert.Empty(r.Warnings);
        Assert.Equal(file, $"{r.Lesson!.Meta.Id}.lesson.md");
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Follow_sizing_and_question_mix_rules(string file)
    {
        var l = Load(file).Lesson!;
        var s = Sizing[l.Meta.Energy];
        Assert.InRange(PaperWords(l.Body), s.Min, s.Max);
        Assert.InRange(l.Questions.Count, s.QMin, s.QMax);
        Assert.True(l.Questions.Select(q => q.Type).Distinct().Count() >= (l.Meta.Energy == 1 ? 2 : 3));
        Assert.InRange(l.Questions.Count(q => !q.InPractice), 1, 2);
        if (l.Meta.Track is "C" or "X") Assert.Contains(l.Questions, q => q is ReflectQuestion);
        if (l.Meta.Track is "P") Assert.Contains(l.Questions, q => q is FillQuestion or OrderQuestion);
        Assert.All(l.Questions, q => Assert.True(q.Hints.Count <= 3));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Use_no_banned_words(string file)
    {
        var hits = Repo.Read($"content/lessons/{file}").Split('\n').Select((t, i) => (Line: i + 1, Text: t)).Where(x => Banned.IsMatch(x.Text)).ToList();
        Assert.Empty(hits);
    }

    [Fact]
    public void Use_every_question_type_across_the_three()
    {
        var used = Repo.BundledLessonFiles().SelectMany(f => Load(f).Lesson!.Questions.Select(q => q.Type)).ToHashSet();
        Assert.Equal(Format.QuestionTypes.Order(), used.Order());
    }

    [Fact]
    public void Generator_prompt_matches_handoff_exactly()
    {
        var prompt = Repo.Read("content/generator-prompt.md").Replace("\r\n", "\n");
        var handoff = Repo.Read("HANDOFF.md").Replace("\r\n", "\n");
        const string begin = "<<<BEGIN FILE: content/generator-prompt.md>>>\n";
        var start = handoff.IndexOf(begin) + begin.Length;
        var end = handoff.IndexOf("<<<END FILE: content/generator-prompt.md>>>");
        Assert.Equal(handoff[start..end], prompt);
    }

    [Fact]
    public void Generator_prompt_example_validates()
    {
        var prompt = Repo.Read("content/generator-prompt.md").Replace("\r\n", "\n");
        var r = Validator.Load(prompt[prompt.IndexOf("````lesson")..], Units);
        Assert.Empty(r.Errors);
        Assert.Empty(r.Warnings);
        Assert.Equal("p-signals-01", r.Lesson!.Meta.Id);
    }

    [Fact]
    public void Curriculum_has_tracks_shares_and_real_prerequisites()
    {
        var c = Curriculum.Parse(Repo.Read("content/curriculum.json"));
        Assert.Equal(["C", "X", "P", "D", "A", "S", "N", "K"], c.Tracks.Select(t => t.Code));
        Assert.Equal(1, c.Tracks.Sum(t => t.Share), 6);
        foreach (var u in c.AllUnits) foreach (var p in u.Prereqs) Assert.Contains(p, Units);
    }

    [Fact]
    public void Embedded_content_matches_the_files() =>
        Assert.Equal(Repo.Read("content/lessons/c-core-loops-01.lesson.md").Replace("\r\n", "\n"), Bundled.Files["lessons/c-core-loops-01.lesson.md"].Replace("\r\n", "\n"));
}
