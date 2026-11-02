// The "learner context" block that goes after the generator prompt (HANDOFF 8).
using System.Globalization;
using System.Text.Json;
using Nordlys.Core.Lesson;

namespace Nordlys.Core.Progress;

public sealed record ContextInput(string Date, int Energy, string Request, IReadOnlyCollection<LessonDoc> Lessons, ProgressData Progress, Curriculum Curriculum, IReadOnlyDictionary<string, int> Levels);

public static class LearnerContext
{
    private const double WeakAvg = 0.6;
    private const int WeakMinAnswers = 3;

    private static string FmtScore(double s)
    {
        var t = s.ToString("0.00", CultureInfo.InvariantCulture);
        return t.EndsWith('0') ? t[..^1] : t;
    }

    public static List<string> WeakTags(IEnumerable<LessonDoc> lessons, ProgressData p)
    {
        var sums = new Dictionary<string, (double Total, int N)>();
        foreach (var l in lessons)
            foreach (var q in l.Questions)
            {
                if (!p.Questions.TryGetValue($"{l.Meta.Id}#{q.Id}", out var qp)) continue;
                var tags = l.Meta.Tags.Concat(q.Tags).Distinct().ToList();
                foreach (var h in qp.History)
                    foreach (var t in tags)
                    {
                        var s = sums.GetValueOrDefault(t);
                        sums[t] = (s.Total + Results.Score(h.Result), s.N + 1);
                    }
            }
        return sums.Where(kv => kv.Value.N >= WeakMinAnswers && kv.Value.Total / kv.Value.N < WeakAvg).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList();
    }

    public static string Build(ContextInput c)
    {
        var p = c.Progress;
        var practiced = c.Lessons.Count(l => p.Lessons.TryGetValue(l.Meta.Id, out var lp) && (lp.Status == "practiced" || lp.Attempts.Count > 0));
        var mastery = c.Curriculum.AllUnits.Where(u => c.Levels.GetValueOrDefault(u.Id) > 0).Select(u => $"{u.Id}={Mastery.LevelNames[c.Levels[u.Id]]}").ToList();
        var recent = p.Lessons.SelectMany(kv => kv.Value.Attempts.Select((a, i) => (Id: kv.Key, A: a, I: i)))
            .OrderByDescending(x => x.A.Date, StringComparer.Ordinal).ThenByDescending(x => x.I)
            .Take(5).Select(x => $"{x.Id} {FmtScore(x.A.Score)}").ToList();
        var weak = WeakTags(c.Lessons, p);
        var seeds = c.Lessons.Where(l => l.Meta.Seed is { } s && (s.Theme != null || s.Verb != null))
            .OrderByDescending(l => p.Lessons.GetValueOrDefault(l.Meta.Id)?.Imported ?? "", StringComparer.Ordinal)
            .Take(10).Select(l => $"{l.Meta.Seed!.Theme ?? "?"}/{l.Meta.Seed.Verb ?? "?"}").ToList();
        var units = c.Curriculum.Tracks.Select(t => $"  {t.Code} ({t.Name}): {string.Join("; ", t.Units.Select(u => $"{u.Id} {u.Title}"))}");
        var request = c.Request.Trim();
        static string Or(List<string> xs, string sep, string none) => xs.Count > 0 ? string.Join(sep, xs) : none;

        return string.Join('\n', new[]
        {
            "=== LEARNER CONTEXT ===",
            $"date: {c.Date}",
            $"energy: {c.Energy}",
            $"request: {(request != "" ? JsonSerializer.Serialize(request, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) : "next")}",
            $"lessons_practiced: {practiced}",
            $"unit_mastery: {Or(mastery, ", ", "none yet")}",
            $"recent (last 5): {Or(recent, " | ", "none yet")}",
            $"weak_tags (avg < {WeakAvg.ToString(CultureInfo.InvariantCulture)}, min {WeakMinAnswers} answers): {Or(weak, ", ", "none")}",
            $"known_terms: {Or(p.Glossary.Keys.ToList(), ", ", "none yet")}",
            $"existing_ids: {Or(c.Lessons.Select(l => l.Meta.Id).Order(StringComparer.Ordinal).ToList(), ", ", "none")}",
            $"used_seeds (last 10): {Or(seeds, ", ", "none")}",
            "curriculum_units:",
        }.Concat(units).Append("=== END CONTEXT ==="));
    }
}
