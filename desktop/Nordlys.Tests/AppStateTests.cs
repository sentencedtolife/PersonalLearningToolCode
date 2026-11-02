using Nordlys.Core;
using Nordlys.Core.Progress;
using Nordlys.Core.Storage;

namespace Nordlys.Tests;

/// <summary>Counts store operations, so the cost of saving is measured rather than assumed.</summary>
internal sealed class CountingStore : IStore
{
    private readonly MemoryStore inner = new();
    public List<string> Ops { get; } = [];
    public string? Root => null;
    public Task<string> ReadTextAsync(string p) { Ops.Add($"read {p}"); return inner.ReadTextAsync(p); }
    public Task WriteTextAsync(string p, string t) { Ops.Add($"write {p}"); return inner.WriteTextAsync(p, t); }
    public Task<IReadOnlyList<string>> ListAsync(string d) { Ops.Add($"list {d}"); return inner.ListAsync(d); }
    public Task<bool> ExistsAsync(string p) { Ops.Add($"exists {p}"); return inner.ExistsAsync(p); }
    public Task RemoveAsync(string p) { Ops.Add($"remove {p}"); return inner.RemoveAsync(p); }
}

public class AppStateTests
{
    [Fact]
    public async Task First_run_copies_bundled_content_and_orders_the_spine()
    {
        var store = new MemoryStore();
        var app = await AppState.LoadAsync(store);
        Assert.Equal(3, app.Lessons.Count);
        Assert.Empty(app.Broken);
        Assert.True(await store.ExistsAsync("generator-prompt.md"));
        Assert.True(await store.ExistsAsync("settings.json"));
        var imported = new[] { "x-next-action-01", "p-godot-tour-01", "c-core-loops-01" }.Select(id => app.Progress.Lessons[id].Imported!).ToList();
        Assert.Equal(imported.Order(StringComparer.Ordinal), imported);
        // Energy 1 with nothing practiced: the X lesson (tier 0, energy 1) comes first.
        Assert.Equal(new NextPick.New("x-next-action-01"), NextLesson.Pick(app.Lessons, app.Progress, 1, app.Levels()));
    }

    [Fact]
    public async Task Later_runs_never_overwrite_user_files()
    {
        var store = new MemoryStore();
        await AppState.LoadAsync(store);
        await store.WriteTextAsync("generator-prompt.md", "my own prompt");
        await store.RemoveAsync("lessons/c-core-loops-01.lesson.md");
        var again = await AppState.LoadAsync(store);
        Assert.Equal("my own prompt", await again.ReadGeneratorPromptAsync());
        Assert.False(again.Lessons.ContainsKey("c-core-loops-01"));
    }

    [Fact]
    public async Task Daily_backup_check_happens_once_then_saves_are_one_write()
    {
        var store = new CountingStore();
        var p = new ProgressData();
        await ProgressStore.SaveAsync(store, p, "2026-11-02");
        await ProgressStore.SaveAsync(store, p, "2026-11-02");
        store.Ops.Clear();
        await ProgressStore.SaveAsync(store, p, "2026-11-02");
        Assert.Equal(["write progress.json"], store.Ops);
        store.Ops.Clear();
        await ProgressStore.SaveAsync(store, p, "2026-11-03");
        Assert.Contains("write backups/progress-2026-11-03.json", store.Ops);
    }

    [Fact]
    public void A_burst_of_saves_becomes_one_write() => SingleThreadContext.Run(async () =>
    {
        var store = new CountingStore();
        var app = new AppState { Store = store };
        await app.SaveProgressAsync();
        store.Ops.Clear();
        app.Progress.Resume.Route = "#/a";
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => app.SaveProgressAsync()));
        Assert.Single(store.Ops, o => o == "write progress.json");
        Assert.Equal("#/a", ProgressStore.ParseProgress(await store.ReadTextAsync("progress.json"))!.Resume.Route);
    });

    [Fact]
    public async Task Prompt_has_generator_text_context_and_request()
    {
        var app = await AppState.LoadAsync(new MemoryStore());
        var text = await app.BuildPromptAsync("teach me signals", 3);
        Assert.StartsWith("# Lesson generator — instructions for Claude", text);
        Assert.Contains("=== LEARNER CONTEXT ===\ndate: ", text);
        Assert.Contains("energy: 3", text);
        Assert.EndsWith("My request: teach me signals\n", text);
    }
}
