// Shared app state, loaded once at start: store, settings, curriculum, lesson library, progress.
using System.Reflection;
using Nordlys.Core.Lesson;
using Nordlys.Core.Progress;
using Nordlys.Core.Storage;

namespace Nordlys.Core;

public sealed record BrokenLesson(string File, IReadOnlyList<Issue> Errors);

/// <summary>Files in content/, keyed by their path inside the data folder (e.g. "lessons/x.lesson.md").</summary>
public static class Bundled
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> files = new(() =>
    {
        var asm = typeof(Bundled).Assembly;
        return asm.GetManifestResourceNames().Where(n => n.StartsWith("content/")).ToDictionary(n => n["content/".Length..].Replace('\\', '/'), n =>
        {
            using var s = asm.GetManifestResourceStream(n)!;
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        });
    });

    public static IReadOnlyDictionary<string, string> Files => files.Value;
}

public sealed class AppState
{
    /// <summary>Order the bundled lessons count as "imported", so Next follows the starting spine.</summary>
    private static readonly string[] BundledOrder = ["x-next-action-01", "p-godot-tour-01", "c-core-loops-01"];

    public required IStore Store { get; init; }
    public Settings Settings { get; private set; } = new();
    public Curriculum Curriculum { get; private set; } = Curriculum.Parse(Bundled.Files["curriculum.json"]);
    public Dictionary<string, LessonDoc> Lessons { get; private set; } = [];
    public List<BrokenLesson> Broken { get; private set; } = [];
    public ProgressData Progress { get; set; } = new();
    /// <summary>One-line notice for Home (e.g. progress was restored from a backup).</summary>
    public string? Notice { get; set; }
    /// <summary>Energy picked on Home in this session.</summary>
    public int? Energy { get; set; }
    /// <summary>When each lesson was opened in this session, for session minutes.</summary>
    public Dictionary<string, DateTime> OpenedAt { get; } = [];

    private async Task<string?> ReadOrNull(string path)
    {
        try { return await Store.ExistsAsync(path) ? await Store.ReadTextAsync(path) : null; }
        catch (IOException) { return null; }
    }

    public static async Task<AppState> LoadAsync(IStore store)
    {
        var s = new AppState { Store = store };
        var settingsText = await s.ReadOrNull("settings.json");
        s.Settings = Settings.Parse(settingsText);

        // First run: copy bundled content into the data folder. Never overwrite on later runs.
        var firstRun = settingsText == null;
        if (firstRun)
        {
            foreach (var (path, text) in Bundled.Files)
                if (!await store.ExistsAsync(path)) await store.WriteTextAsync(path, text);
            await s.SaveSettingsAsync();
        }

        try
        {
            s.Curriculum = Curriculum.Parse(await s.ReadOrNull("curriculum.json") ?? Bundled.Files["curriculum.json"]);
        }
        catch (Exception e) when (e is FormatException or System.Text.Json.JsonException)
        {
            s.Notice = "curriculum.json could not be read, so Nordlys is using the built-in curriculum.";
        }

        var (progress, notice) = await ProgressStore.LoadAsync(store);
        s.Progress = progress;
        s.Notice = notice ?? s.Notice;

        await s.ReloadLessonsAsync();
        if (firstRun)
        {
            var baseTime = DateTime.UtcNow;
            for (var i = 0; i < BundledOrder.Length; i++)
                if (s.Lessons.ContainsKey(BundledOrder[i])) s.Progress.Lesson(BundledOrder[i]).Imported = baseTime.AddMilliseconds(i).ToString("O");
            await s.SaveProgressAsync();
        }
        return s;
    }

    public HashSet<string> KnownUnits() => Curriculum.AllUnits.Select(u => u.Id).ToHashSet();

    public async Task ReloadLessonsAsync()
    {
        var files = (await Store.ListAsync("lessons")).Where(f => f.EndsWith(".lesson.md")).Order(StringComparer.Ordinal).ToList();
        // Read all files at once; parsing happens after.
        var texts = await Task.WhenAll(files.Select(async f => { try { return await Store.ReadTextAsync($"lessons/{f}"); } catch (IOException) { return ""; } }));
        var units = KnownUnits();
        var lessons = new Dictionary<string, LessonDoc>();
        var broken = new List<BrokenLesson>();
        var changed = false;
        for (var i = 0; i < files.Count; i++)
        {
            var r = Validator.Load(texts[i], units);
            if (r.Lesson is null) { broken.Add(new BrokenLesson(files[i], r.Errors)); continue; }
            lessons[r.Lesson.Meta.Id] = r.Lesson;
            var lp = Progress.Lesson(r.Lesson.Meta.Id);
            if (lp.Imported == null) { lp.Imported = DateTime.UtcNow.ToString("O"); changed = true; }
        }
        Lessons = lessons;
        Broken = broken;
        if (changed) await SaveProgressAsync();
    }

    public Dictionary<string, int> Levels() => Mastery.AllLevels(Curriculum.AllUnits.Select(u => u.Id), Lessons.Values, Progress);

    private Task? saving;
    private bool dirty;

    /// <summary>
    /// Two writes never overlap, and a burst of calls (e.g. open lesson + record the route) becomes one write.
    /// The returned task finishes once the latest state is on disk.
    /// </summary>
    public Task SaveProgressAsync()
    {
        dirty = true;
        return saving ??= SaveLoop();
    }

    private async Task SaveLoop()
    {
        try
        {
            while (dirty)
            {
                await Task.Yield(); // let calls made in the same turn join this write
                dirty = false;
                await ProgressStore.SaveAsync(Store, Progress);
            }
        }
        catch (IOException e)
        {
            Notice = $"Saving progress failed: {e.Message}";
        }
        finally
        {
            saving = null;
        }
    }

    public Task SaveSettingsAsync() => Store.WriteTextAsync("settings.json", Settings.Serialize());

    public async Task SaveLessonFileAsync(string id, string text)
    {
        await Store.WriteTextAsync($"lessons/{id}.lesson.md", text);
        await ReloadLessonsAsync();
    }

    public async Task<string> ReadGeneratorPromptAsync() => await ReadOrNull("generator-prompt.md") ?? Bundled.Files["generator-prompt.md"];

    public Task ResetGeneratorPromptAsync() => Store.WriteTextAsync("generator-prompt.md", Bundled.Files["generator-prompt.md"]);

    public async Task<string> BuildPromptAsync(string request, int energy)
    {
        var prompt = await ReadGeneratorPromptAsync();
        var context = LearnerContext.Build(new ContextInput(Dates.Today(), energy, request, Lessons.Values, Progress, Curriculum, Levels()));
        var req = request.Trim() != "" ? request.Trim() : "next (choose the next lesson in the curriculum, using the rules above)";
        return $"{prompt.TrimEnd()}\n\n{context}\n\nMy request: {req}\n";
    }

    /// <summary>Marks the lesson as opened: new -> read, remembers its glossary terms and when it was opened.</summary>
    public void OpenLesson(LessonDoc lesson)
    {
        var id = lesson.Meta.Id;
        var lp = Progress.Lesson(id);
        if (lp.Status == "new") lp.Status = "read";
        lp.LastOpened = DateTime.UtcNow.ToString("O");
        foreach (var g in lesson.Meta.Glossary)
            Progress.Glossary.TryAdd(g.Term.ToLowerInvariant(), new KnownTerm(g.Definition, id));
        OpenedAt.TryAdd(id, DateTime.Now);
        _ = SaveProgressAsync();
    }

    public int DueCount() => Leitner.ReviewQueue(Progress, Lessons, Dates.Today(), int.MaxValue).Count;

    /// <summary>A lesson reopens in the mode it was left in.</summary>
    public string LessonRoute(string id) => Progress.Lessons.GetValueOrDefault(id)?.LastPosition?.View == "practice" ? $"#/lesson/{id}/practice" : $"#/lesson/{id}";

    public static string AppVersion => typeof(AppState).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0";
}
