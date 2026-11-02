// Leitner scheduling (HANDOFF 7.2), unit mastery (7.3), next-lesson choice (7.4).
using Nordlys.Core.Lesson;

namespace Nordlys.Core.Progress;

public static class Leitner
{
    /// <summary>Days until the next review, for boxes 1–5.</summary>
    public static readonly int[] Intervals = [1, 2, 4, 8, 16];
    public const int ReviewLimit = 15;

    public static int Interval(int box) => Intervals[Math.Clamp(box, 1, 5) - 1];
    public static int BoxAfterFirstPractice(double score) => score == 1 ? 2 : 1;
    public static int BoxAfterReview(int box, double score) => score == 1 ? Math.Min(box + 1, 5) : score == 0 ? 1 : box;

    /// <summary>The first answer ever uses the first-practice rule; later ones use the review rule.</summary>
    public static QuestionProgress RecordAnswer(ProgressData p, string key, string result, string date)
    {
        var score = Results.Score(result);
        p.Questions.TryGetValue(key, out var existing);
        var box = existing != null ? BoxAfterReview(existing.Box, score) : BoxAfterFirstPractice(score);
        var q = existing ?? new QuestionProgress { FirstSeen = date };
        q.Box = box;
        q.Due = Dates.AddDays(date, Interval(box));
        q.History.Add(new HistoryEntry(date, result));
        p.Questions[key] = q;
        return q;
    }

    public sealed record DueItem(string LessonId, string QuestionId, string Due);

    public static (string LessonId, string QuestionId) SplitKey(string key)
    {
        var i = key.IndexOf('#');
        return (key[..i], key[(i + 1)..]);
    }

    /// <summary>Due questions, oldest first. Skips tasks and questions whose lesson or id no longer exists.</summary>
    public static List<DueItem> ReviewQueue(ProgressData p, IReadOnlyDictionary<string, LessonDoc> lessons, string date, int limit = ReviewLimit)
    {
        var items = new List<DueItem>();
        foreach (var (key, q) in p.Questions)
        {
            if (string.CompareOrdinal(q.Due, date) > 0) continue;
            var (lessonId, questionId) = SplitKey(key);
            if (!lessons.TryGetValue(lessonId, out var lesson)) continue;
            var question = lesson.Questions.FirstOrDefault(x => x.Id == questionId);
            if (question is null or TaskQuestion) continue;
            items.Add(new DueItem(lessonId, questionId, q.Due));
        }
        return items.OrderBy(i => i.Due, StringComparer.Ordinal).Take(limit).ToList();
    }
}

public static class Mastery
{
    public static readonly string[] LevelNames = ["Not started", "Familiar", "Proficient", "Mastered"];
    public static readonly string[] LevelRules =
    [
        "Not started: no lesson in this unit practiced with a score of 70% or more yet.",
        "Familiar: at least 1 lesson in this unit practiced with a score of 70% or more.",
        "Proficient: 5 review answers in a row in this unit, each at least 2 days after you first saw that question, averaging 80% or more.",
        "Mastered: 8 review answers in a row in this unit, each at least 7 days after you first saw that question, averaging 90% or more.",
    ];
    public const double FamiliarScore = 0.7;

    private sealed record Answer(string Date, int Gap, double Score);

    /// <summary>True if some run of n consecutive answers averages at least avg. Once true, stays true as history grows.</summary>
    private static bool SomeRunAverages(List<Answer> answers, int n, double avg)
    {
        if (answers.Count < n) return false;
        double sum = 0;
        for (var i = 0; i < answers.Count; i++)
        {
            sum += answers[i].Score;
            if (i >= n) sum -= answers[i - n].Score;
            if (i >= n - 1 && sum / n >= avg - 1e-9) return true;
        }
        return false;
    }

    public static int UnitLevel(string unitId, IEnumerable<LessonDoc> lessons, ProgressData p)
    {
        var inUnit = lessons.Where(l => l.Meta.Unit == unitId).ToList();
        var answers = new List<Answer>();
        foreach (var l in inUnit)
            foreach (var q in l.Questions)
                if (p.Questions.TryGetValue($"{l.Meta.Id}#{q.Id}", out var qp))
                    foreach (var h in qp.History) answers.Add(new Answer(h.Date, Dates.DaysBetween(qp.FirstSeen, h.Date), Results.Score(h.Result)));
        answers = answers.OrderBy(a => a.Date, StringComparer.Ordinal).ToList(); // stable

        if (SomeRunAverages(answers.Where(a => a.Gap >= 7).ToList(), 8, 0.9)) return 3;
        if (SomeRunAverages(answers.Where(a => a.Gap >= 2).ToList(), 5, 0.8)) return 2;
        var familiar = inUnit.Any(l => p.Lessons.TryGetValue(l.Meta.Id, out var lp) && lp.Attempts.Any(a => a.Score >= FamiliarScore));
        return familiar ? 1 : 0;
    }

    public static Dictionary<string, int> AllLevels(IEnumerable<string> unitIds, IReadOnlyCollection<LessonDoc> lessons, ProgressData p) =>
        unitIds.ToDictionary(id => id, id => UnitLevel(id, lessons, p));
}

public abstract record NextPick
{
    public sealed record Resume(string LessonId) : NextPick;
    public sealed record New(string LessonId) : NextPick;
    public sealed record None : NextPick;
}

public static class NextLesson
{
    /// <summary>A prerequisite is met if it is a practiced lesson, or a unit at Familiar or above. Unknown ids are not met.</summary>
    public static bool PrerequisitesMet(LessonDoc lesson, IReadOnlyDictionary<string, LessonDoc> lessons, ProgressData p, IReadOnlyDictionary<string, int> levels) =>
        lesson.Meta.Prerequisites.All(id => lessons.ContainsKey(id)
            ? p.Lessons.TryGetValue(id, out var lp) && lp.Status == "practiced"
            : levels.TryGetValue(id, out var lv) && lv >= 1);

    public static List<string> MissingPrerequisites(LessonDoc lesson, IReadOnlyDictionary<string, LessonDoc> lessons, IReadOnlyDictionary<string, int> levels) =>
        lesson.Meta.Prerequisites.Where(id => !lessons.ContainsKey(id) && !levels.ContainsKey(id)).ToList();

    public static NextPick Pick(IReadOnlyDictionary<string, LessonDoc> lessons, ProgressData p, int energy, IReadOnlyDictionary<string, int> levels)
    {
        LessonProgress? Lp(string id) => p.Lessons.GetValueOrDefault(id);

        // 1. A lesson in progress: the most recently opened one.
        var inProgress = lessons.Values.Where(l => Lp(l.Meta.Id)?.Status == "read")
            .OrderByDescending(l => Lp(l.Meta.Id)?.LastOpened ?? "", StringComparer.Ordinal).FirstOrDefault();
        if (inProgress != null) return new NextPick.Resume(inProgress.Meta.Id);

        // 2. Unpracticed lessons that fit the energy: prerequisites met first, then lower tier, then oldest imported.
        var next = lessons.Values
            .Where(l => (Lp(l.Meta.Id)?.Status ?? "new") != "practiced" && l.Meta.Energy <= energy)
            .OrderByDescending(l => PrerequisitesMet(l, lessons, p, levels))
            .ThenBy(l => l.Meta.Tier)
            .ThenBy(l => Lp(l.Meta.Id)?.Imported ?? "", StringComparer.Ordinal)
            .ThenBy(l => l.Meta.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        return next != null ? new NextPick.New(next.Meta.Id) : new NextPick.None();
    }
}
