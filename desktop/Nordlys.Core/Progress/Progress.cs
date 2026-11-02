// progress.json: the same JSON shape as HANDOFF section 6 (and as the earlier web version), so data carries over.
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nordlys.Core.Storage;

namespace Nordlys.Core.Progress;

public static class Results
{
    public const string Correct = "correct", CorrectHinted = "correct-hinted", Wrong = "wrong";
    public const string SelfGot = "self-got", SelfPartly = "self-partly", SelfNot = "self-not";
    /// <summary>A task question in a practice run (not scored).</summary>
    public const string Task = "task";

    public static double Score(string result) => result switch
    {
        Correct or SelfGot => 1,
        CorrectHinted or SelfPartly => 0.5,
        _ => 0,
    };
}

public sealed class Position
{
    public string View { get; set; } = "paper"; // "paper" | "practice"
    public double Scroll { get; set; }
}

public sealed class PracticeRun
{
    public Dictionary<string, string> Results { get; set; } = [];
    public bool Finished { get; set; }
}

public sealed record AttemptRecord(string Date, double Score, int Total);

public sealed class LessonProgress
{
    public string Status { get; set; } = "new"; // "new" | "read" | "practiced"
    public Position? LastPosition { get; set; }
    public PracticeRun? Run { get; set; }
    public List<AttemptRecord> Attempts { get; set; } = [];
    public string? Imported { get; set; }
    public string? LastOpened { get; set; }
}

public sealed record HistoryEntry(string Date, string Result);

public sealed class QuestionProgress
{
    public int Box { get; set; }
    public string Due { get; set; } = "";
    public string FirstSeen { get; set; } = "";
    public List<HistoryEntry> History { get; set; } = [];
}

public sealed record SessionRecord(string Date, int Energy, string LessonId, int Minutes);
public sealed record KnownTerm(string Definition, string FirstLesson);

public sealed class ResumeInfo { public string Route { get; set; } = "#/home"; }

public sealed class ProgressData
{
    public int Version { get; set; } = 1;
    public Dictionary<string, LessonProgress> Lessons { get; set; } = [];
    /// <summary>Key: "&lt;lessonId&gt;#&lt;questionId&gt;".</summary>
    public Dictionary<string, QuestionProgress> Questions { get; set; } = [];
    public Dictionary<string, string> TaskChecks { get; set; } = [];
    public List<SessionRecord> Sessions { get; set; } = [];
    public Dictionary<string, KnownTerm> Glossary { get; set; } = [];
    public ResumeInfo Resume { get; set; } = new();

    public LessonProgress Lesson(string id)
    {
        if (!Lessons.TryGetValue(id, out var lp)) Lessons[id] = lp = new LessonProgress();
        return lp;
    }
}

public static class Dates
{
    /// <summary>Local calendar date as yyyy-MM-dd.</summary>
    public static string Today(DateTime? d = null) => (d ?? DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime Parse(string date) => DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string AddDays(string date, int n) => Today(Parse(date).AddDays(n));

    public static int DaysBetween(string from, string to) => (int)Math.Round((Parse(to) - Parse(from)).TotalDays);
}

public static class ProgressStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private const string File = "progress.json";
    private const string BackupDir = "backups";
    private const int KeepBackups = 14;
    private static readonly System.Text.RegularExpressions.Regex BackupName = new(@"^progress-\d{4}-\d{2}-\d{2}\.json$");

    /// <summary>Returns null if the text is not a usable progress file.</summary>
    public static ProgressData? ParseProgress(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("lessons", out var l) || l.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("questions", out var q) || q.ValueKind != JsonValueKind.Object) return null;
            return JsonSerializer.Deserialize<ProgressData>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(ProgressData p) => JsonSerializer.Serialize(p, Json);

    public static async Task<(ProgressData Progress, string? Notice)> LoadAsync(IStore store, string? date = null)
    {
        date ??= Dates.Today();
        if (!await store.ExistsAsync(File)) return (new ProgressData(), null);
        var text = await TryRead(store, File);
        if (ParseProgress(text) is { } parsed) return (parsed, null);

        // Damaged: keep a copy for inspection, then fall back to the newest valid backup.
        try { await store.WriteTextAsync($"{BackupDir}/progress-damaged-{date}.json", text); } catch (IOException) { }
        foreach (var f in (await store.ListAsync(BackupDir)).Where(f => BackupName.IsMatch(f)).OrderDescending())
        {
            if (ParseProgress(await TryRead(store, $"{BackupDir}/{f}")) is { } p)
                return (p, $"progress.json was damaged, so Nordlys loaded the backup from {f[9..19]}.");
        }
        return (new ProgressData(), "progress.json was damaged and no backup could be read, so progress starts empty.");
    }

    private static async Task<string> TryRead(IStore store, string path)
    {
        try { return await store.ReadTextAsync(path); } catch (IOException) { return ""; }
    }

    /// <summary>Store -> the date its daily backup was last checked, so later saves that day skip the check.</summary>
    private static readonly ConditionalWeakTable<IStore, string> BackupChecked = new();

    public static async Task SaveAsync(IStore store, ProgressData progress, string? date = null)
    {
        date ??= Dates.Today();
        var backup = $"{BackupDir}/progress-{date}.json";
        var checkedToday = BackupChecked.TryGetValue(store, out var d) && d == date;
        if (!checkedToday && !await store.ExistsAsync(backup) && await store.ExistsAsync(File))
        {
            // First save of the day: keep the end state of the last day the app was used.
            var previous = await store.ReadTextAsync(File);
            if (ParseProgress(previous) != null)
            {
                await store.WriteTextAsync(backup, previous);
                var all = (await store.ListAsync(BackupDir)).Where(f => BackupName.IsMatch(f)).Order().ToList();
                foreach (var old in all.Take(Math.Max(0, all.Count - KeepBackups))) await store.RemoveAsync($"{BackupDir}/{old}");
            }
        }
        BackupChecked.AddOrUpdate(store, date);
        await store.WriteTextAsync(File, Serialize(progress));
    }

    /// <summary>After a lesson is replaced: keep progress for question ids that still exist, drop the rest.</summary>
    public static List<string> PruneLessonQuestions(ProgressData p, string lessonId, IEnumerable<string> keepIds)
    {
        var keep = keepIds.ToHashSet();
        var prefix = $"{lessonId}#";
        var dropped = new List<string>();
        foreach (var key in p.Questions.Keys.Where(k => k.StartsWith(prefix) && !keep.Contains(k[prefix.Length..])).ToList()) { p.Questions.Remove(key); dropped.Add(key); }
        foreach (var key in p.TaskChecks.Keys.Where(k => k.StartsWith(prefix) && !keep.Contains(k[prefix.Length..])).ToList()) { p.TaskChecks.Remove(key); dropped.Add(key); }
        if (p.Lessons.TryGetValue(lessonId, out var lp) && lp.Run != null)
            foreach (var qid in lp.Run.Results.Keys.Where(k => !keep.Contains(k)).ToList()) lp.Run.Results.Remove(qid);
        return dropped;
    }
}
