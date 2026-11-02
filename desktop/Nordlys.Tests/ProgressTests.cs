using Nordlys.Core;
using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;
using Nordlys.Core.Storage;

namespace Nordlys.Tests;

public class ProgressTests
{
    private static Question Q(string id, string type = "text", string[]? tags = null) => type == "task"
        ? new TaskQuestion { Id = id, Prompt = "x", Explanation = "x", Instructions = "x", TaskDoneMeans = "x", Tags = tags ?? [], InPractice = true }
        : new TextQuestion { Id = id, Prompt = "x", Explanation = "x", Accept = ["x"], Tags = tags ?? [], InPractice = true };

    private static LessonDoc L(string id, Func<LessonMeta, LessonMeta>? edit = null, IReadOnlyList<Question>? qs = null)
    {
        var meta = new LessonMeta { Id = id, Title = id, Track = "C", Unit = "C.loops", Tier = 1, Energy = 2, Minutes = 20, PaperType = "concept", DoneMeans = "x", Summary = "x" };
        return new LessonDoc(edit?.Invoke(meta) ?? meta, "", qs ?? [Q("q1"), Q("q2")]);
    }

    [Fact]
    public void Dates()
    {
        Assert.Equal("2026-11-02", Core.Progress.Dates.Today(new DateTime(2026, 11, 2)));
        Assert.Equal("2026-12-01", Core.Progress.Dates.AddDays("2026-11-30", 1));
        Assert.Equal("2027-01-16", Core.Progress.Dates.AddDays("2026-12-31", 16));
        Assert.Equal(7, Core.Progress.Dates.DaysBetween("2026-11-02", "2026-11-09"));
        Assert.Equal(2, Core.Progress.Dates.DaysBetween("2026-03-28", "2026-03-30"));
    }

    [Fact]
    public void Leitner_boxes()
    {
        Assert.Equal([1, 2, 4, 8, 16], new[] { 1, 2, 3, 4, 5 }.Select(Leitner.Interval));
        Assert.Equal((2, 1, 1), (Leitner.BoxAfterFirstPractice(1), Leitner.BoxAfterFirstPractice(0.5), Leitner.BoxAfterFirstPractice(0)));
        Assert.Equal((3, 5, 3, 1), (Leitner.BoxAfterReview(2, 1), Leitner.BoxAfterReview(5, 1), Leitner.BoxAfterReview(3, 0.5), Leitner.BoxAfterReview(4, 0)));
    }

    [Fact]
    public void RecordAnswer_sets_box_due_history()
    {
        var p = new ProgressData();
        var a = Leitner.RecordAnswer(p, "l#q1", "correct", "2026-11-02");
        Assert.Equal((2, "2026-11-04", "2026-11-02"), (a.Box, a.Due, a.FirstSeen));
        Leitner.RecordAnswer(p, "l#q1", "correct", "2026-11-04");
        Assert.Equal((3, "2026-11-08"), (p.Questions["l#q1"].Box, p.Questions["l#q1"].Due));
        Leitner.RecordAnswer(p, "l#q1", "self-partly", "2026-11-08");
        Assert.Equal((3, "2026-11-12"), (p.Questions["l#q1"].Box, p.Questions["l#q1"].Due));
        Leitner.RecordAnswer(p, "l#q1", "wrong", "2026-11-12");
        Assert.Equal((1, "2026-11-13"), (p.Questions["l#q1"].Box, p.Questions["l#q1"].Due));
        Assert.Equal(["correct", "correct", "self-partly", "wrong"], p.Questions["l#q1"].History.Select(h => h.Result));
    }

    [Fact]
    public void Review_queue_rules()
    {
        var l = L("l", null, [.. Enumerable.Range(0, 20).Select(i => Q($"q{i}")), Q("t", "task")]);
        var lessons = new Dictionary<string, LessonDoc> { ["l"] = l };
        var p = new ProgressData();
        for (var i = 0; i < 20; i++) p.Questions[$"l#q{i}"] = new QuestionProgress { Box = 1, Due = Core.Progress.Dates.AddDays("2026-11-01", -i), FirstSeen = "2026-10-01" };
        p.Questions["l#t"] = new QuestionProgress { Box = 1, Due = "2026-10-01", FirstSeen = "2026-10-01" };
        p.Questions["gone#q1"] = new QuestionProgress { Box = 1, Due = "2026-10-01", FirstSeen = "2026-10-01" };
        p.Questions["l#q0"].Due = "2026-11-03";
        var queue = Leitner.ReviewQueue(p, lessons, "2026-11-02");
        Assert.Equal(15, queue.Count);
        Assert.Equal(new Leitner.DueItem("l", "q19", "2026-10-13"), queue[0]);
        Assert.Equal(queue.Select(x => x.Due).Order(StringComparer.Ordinal), queue.Select(x => x.Due));
        Assert.DoesNotContain(queue, x => x.QuestionId is "t" or "q0" || x.LessonId == "gone");
    }

    private static readonly LessonDoc Eight = L("l", null, [.. Enumerable.Range(0, 8).Select(i => Q($"q{i}"))]);

    private static ProgressData History(IEnumerable<(string Q, string Date, string Result)> entries)
    {
        var p = new ProgressData();
        foreach (var (qid, date, result) in entries)
        {
            var key = $"l#{qid}";
            if (!p.Questions.ContainsKey(key)) p.Questions[key] = new QuestionProgress { Box = 1, Due = date, FirstSeen = "2026-11-01" };
            p.Questions[key].History.Add(new HistoryEntry(date, result));
        }
        return p;
    }

    private static IEnumerable<(string, string, string)> Late(int n, string result, string date = "2026-11-03") => Enumerable.Range(0, n).Select(i => ($"q{i}", date, result));

    [Fact]
    public void Mastery_not_started_and_familiar()
    {
        var p = new ProgressData();
        Assert.Equal(0, Mastery.UnitLevel("C.loops", [Eight], p));
        p.Lessons["l"] = new LessonProgress { Status = "practiced", Attempts = [new AttemptRecord("2026-11-01", 0.6, 5)] };
        Assert.Equal(0, Mastery.UnitLevel("C.loops", [Eight], p));
        p.Lessons["l"].Attempts.Add(new AttemptRecord("2026-11-02", 0.7, 5));
        Assert.Equal(1, Mastery.UnitLevel("C.loops", [Eight], p));
    }

    [Fact]
    public void Mastery_proficient()
    {
        Assert.Equal(2, Mastery.UnitLevel("C.loops", [Eight], History(Late(5, "correct"))));
        Assert.Equal(0, Mastery.UnitLevel("C.loops", [Eight], History(Late(4, "correct"))));
        Assert.Equal(0, Mastery.UnitLevel("C.loops", [Eight], History(Late(6, "correct", "2026-11-02"))));
        Assert.Equal(2, Mastery.UnitLevel("C.loops", [Eight], History(Late(4, "correct").Append(("q7", "2026-11-03", "correct-hinted")))));
        Assert.Equal(0, Mastery.UnitLevel("C.loops", [Eight], History(Late(3, "correct").Append(("q6", "2026-11-03", "wrong")).Append(("q7", "2026-11-03", "wrong")))));
    }

    [Fact]
    public void Mastery_mastered_and_only_goes_up()
    {
        Assert.Equal(3, Mastery.UnitLevel("C.loops", [Eight], History(Late(8, "correct", "2026-11-08"))));
        Assert.Equal(2, Mastery.UnitLevel("C.loops", [Eight], History(Late(8, "correct", "2026-11-07"))));
        var bad = Enumerable.Range(0, 10).Select(i => ($"q{i % 8}", "2026-11-05", "wrong"));
        Assert.Equal(2, Mastery.UnitLevel("C.loops", [Eight], History(Late(5, "correct").Concat(bad))));
        Assert.Equal(0, Mastery.UnitLevel("C.verbs", [Eight], History(Late(5, "correct"))));
    }

    private static readonly Dictionary<string, int> Levels = new() { ["C.verbs"] = 0, ["C.loops"] = 0, ["P.tour"] = 1 };

    [Fact]
    public void Next_resumes_most_recent_in_progress()
    {
        var lessons = new Dictionary<string, LessonDoc> { ["a"] = L("a", m => m with { Energy = 3 }), ["b"] = L("b", m => m with { Energy = 3 }), ["c"] = L("c", m => m with { Energy = 1 }) };
        var p = new ProgressData();
        p.Lessons["a"] = new LessonProgress { Status = "read", LastOpened = "2026-11-01T10:00:00Z" };
        p.Lessons["b"] = new LessonProgress { Status = "read", LastOpened = "2026-11-02T10:00:00Z" };
        Assert.Equal(new NextPick.Resume("b"), NextLesson.Pick(lessons, p, 1, Levels));
    }

    [Fact]
    public void Next_respects_energy()
    {
        var lessons = new Dictionary<string, LessonDoc> { ["a"] = L("a", m => m with { Energy = 3 }), ["b"] = L("b", m => m with { Energy = 2 }) };
        Assert.Equal(new NextPick.New("b"), NextLesson.Pick(lessons, new ProgressData(), 2, Levels));
        Assert.Equal(new NextPick.None(), NextLesson.Pick(lessons, new ProgressData(), 1, Levels));
    }

    [Fact]
    public void Next_prefers_met_prerequisites_then_tier_then_oldest()
    {
        var lessons = new Dictionary<string, LessonDoc>
        {
            ["unmet"] = L("unmet", m => m with { Tier = 0, Prerequisites = ["C.verbs"] }),
            ["met-t2"] = L("met-t2", m => m with { Tier = 2, Prerequisites = ["P.tour"] }),
            ["met-t1-new"] = L("met-t1-new", m => m with { Tier = 1 }),
            ["met-t1-old"] = L("met-t1-old", m => m with { Tier = 1 }),
        };
        var p = new ProgressData();
        p.Lessons["met-t1-new"] = new LessonProgress { Imported = "2026-11-02T00:00:00Z" };
        p.Lessons["met-t1-old"] = new LessonProgress { Imported = "2026-11-01T00:00:00Z" };
        Assert.Equal(new NextPick.New("met-t1-old"), NextLesson.Pick(lessons, p, 3, Levels));
        p.Lessons["met-t1-old"].Status = "practiced";
        p.Lessons["met-t1-new"].Status = "practiced";
        Assert.Equal(new NextPick.New("met-t2"), NextLesson.Pick(lessons, p, 3, Levels));
        p.Lessons["met-t2"] = new LessonProgress { Status = "practiced" };
        Assert.Equal(new NextPick.New("unmet"), NextLesson.Pick(lessons, p, 3, Levels));
    }

    [Fact]
    public void Weak_tags_need_three_answers_under_point_six()
    {
        var l = L("l", m => m with { Tags = ["loops"] }, [Q("q1", "text", ["vectors"]), Q("q2", "text", ["delta-time"])]);
        var p = new ProgressData();
        p.Questions["l#q1"] = new QuestionProgress { History = [new("d", "wrong"), new("d", "wrong"), new("d", "correct")] };
        p.Questions["l#q2"] = new QuestionProgress { History = [new("d", "wrong"), new("d", "wrong")] };
        Assert.Equal(["loops", "vectors"], LearnerContext.WeakTags([l], p));
    }

    [Fact]
    public void Learner_context_has_every_field()
    {
        var cur = Curriculum.Parse(Repo.Read("content/curriculum.json"));
        var p = new ProgressData();
        var l = L("c-core-loops-01", m => m with { Seed = new Seed("lighthouse", "push", null) });
        p.Lessons["c-core-loops-01"] = new LessonProgress { Status = "practiced", Imported = "2026-11-01", Attempts = [new("2026-11-02", 0.83, 6), new("2026-11-03", 1, 6)] };
        p.Glossary["core loop"] = new KnownTerm("x", "c-core-loops-01");
        var text = LearnerContext.Build(new ContextInput("2026-11-03", 2, "teach me signals in Godot", [l], p, cur, new Dictionary<string, int> { ["C.loops"] = 1 }));
        Assert.Contains("=== LEARNER CONTEXT ===\ndate: 2026-11-03\nenergy: 2\nrequest: \"teach me signals in Godot\"\nlessons_practiced: 1", text);
        Assert.Contains("unit_mastery: C.loops=Familiar", text);
        Assert.Contains("recent (last 5): c-core-loops-01 1.0 | c-core-loops-01 0.83", text);
        Assert.Contains("weak_tags (avg < 0.6, min 3 answers): none", text);
        Assert.Contains("known_terms: core loop", text);
        Assert.Contains("existing_ids: c-core-loops-01", text);
        Assert.Contains("used_seeds (last 10): lighthouse/push", text);
        Assert.Contains("  P (Programming & Game Maths in Godot): P.tour Godot tour: editor, nodes, scenes; P.vars Variables and types;", text);
        Assert.EndsWith("=== END CONTEXT ===", text);
        Assert.Contains("request: next", LearnerContext.Build(new ContextInput("2026-11-03", 1, "  ", [], new ProgressData(), cur, new Dictionary<string, int>())));
    }

    [Fact]
    public async Task Backups_one_per_day_of_the_previous_state_keep_14()
    {
        var store = new MemoryStore();
        var p = new ProgressData();
        await ProgressStore.SaveAsync(store, p, "2026-11-01");
        p.Sessions.Add(new SessionRecord("2026-11-01", 1, "x", 5));
        await ProgressStore.SaveAsync(store, p, "2026-11-01");
        Assert.Empty(await store.ListAsync("backups"));
        await ProgressStore.SaveAsync(store, p, "2026-11-02");
        Assert.Equal(["progress-2026-11-02.json"], await store.ListAsync("backups"));
        for (var d = 3; d <= 20; d++) await ProgressStore.SaveAsync(store, p, $"2026-11-{d:00}");
        var backups = await store.ListAsync("backups");
        Assert.Equal(14, backups.Count);
        Assert.Equal("progress-2026-11-07.json", backups[0]);
        var (loaded, notice) = await ProgressStore.LoadAsync(store);
        Assert.Null(notice);
        Assert.Single(loaded.Sessions);
    }

    [Fact]
    public async Task Corrupt_progress_loads_newest_valid_backup()
    {
        var store = new MemoryStore();
        var good = new ProgressData();
        good.Sessions.Add(new SessionRecord("2026-11-01", 1, "x", 5));
        await store.WriteTextAsync("backups/progress-2026-10-30.json", ProgressStore.Serialize(new ProgressData()));
        await store.WriteTextAsync("backups/progress-2026-10-31.json", ProgressStore.Serialize(good));
        await store.WriteTextAsync("backups/progress-2026-11-01.json", "{broken");
        await store.WriteTextAsync("progress.json", "{\"lessons\": ");
        var (p, notice) = await ProgressStore.LoadAsync(store, "2026-11-02");
        Assert.Single(p.Sessions);
        Assert.Equal("progress.json was damaged, so Nordlys loaded the backup from 2026-10-31.", notice);
        Assert.True(await store.ExistsAsync("backups/progress-damaged-2026-11-02.json"));
    }

    [Fact]
    public void Reads_progress_written_by_the_earlier_web_version()
    {
        const string web = """{"version":1,"lessons":{"x-next-action-01":{"status":"read","attempts":[{"date":"2026-11-02","score":0.75,"total":4}],"imported":"2026-11-02T02:33:18.124Z","lastOpened":"2026-11-02T02:39:49.668Z","lastPosition":{"view":"practice","scroll":0},"run":{"results":{"q1":"correct-hinted"},"finished":false}}},"questions":{"x-next-action-01#q1":{"box":1,"due":"2026-11-03","firstSeen":"2026-11-02","history":[{"date":"2026-11-02","result":"correct-hinted"}]}},"taskChecks":{"p-godot-tour-01#q7":"2026-11-02"},"sessions":[],"glossary":{"core loop":{"definition":"x","firstLesson":"c-core-loops-01"}},"resume":{"route":"#/lesson/x-next-action-01/practice"}}""";
        var p = ProgressStore.ParseProgress(web)!;
        Assert.Equal(0.75, p.Lessons["x-next-action-01"].Attempts[0].Score);
        Assert.Equal("practice", p.Lessons["x-next-action-01"].LastPosition!.View);
        Assert.Equal("correct-hinted", p.Lessons["x-next-action-01"].Run!.Results["q1"]);
        Assert.Equal(1, p.Questions["x-next-action-01#q1"].Box);
        Assert.Equal("#/lesson/x-next-action-01/practice", p.Resume.Route);
        Assert.Contains("\"firstSeen\": \"2026-11-02\"", ProgressStore.Serialize(p));
    }

    [Fact]
    public void Prune_keeps_existing_question_ids()
    {
        var p = new ProgressData();
        p.Questions["l#q1"] = new QuestionProgress { Box = 3 };
        p.Questions["l#q2"] = new QuestionProgress { Box = 2 };
        p.Questions["other#q2"] = new QuestionProgress { Box = 2 };
        p.TaskChecks["l#t1"] = "2026-11-01";
        p.Lessons["l"] = new LessonProgress { Status = "read", Run = new PracticeRun { Results = new() { ["q1"] = "correct", ["q2"] = "wrong" } } };
        var dropped = ProgressStore.PruneLessonQuestions(p, "l", ["q1", "q3"]);
        Assert.Equal(["l#q2", "l#t1"], dropped.Order());
        Assert.Equal(["l#q1", "other#q2"], p.Questions.Keys.Order());
        Assert.Equal(["q1"], p.Lessons["l"].Run!.Results.Keys);
    }
}
