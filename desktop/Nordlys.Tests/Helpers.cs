using Nordlys.Core;

namespace Nordlys.Tests;

internal static class Repo
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HANDOFF.md"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (HANDOFF.md) not found");
        }
    }

    public static string Read(string rel) => File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static string[] BundledLessonFiles() =>
        Directory.GetFiles(Path.Combine(Root, "content", "lessons"), "*.lesson.md").Select(Path.GetFileName).OfType<string>().Order().ToArray();

    public static HashSet<string> KnownUnits() => Curriculum.Parse(Read("content/curriculum.json")).AllUnits.Select(u => u.Id).ToHashSet();
}

/// <summary>Runs async code on one thread, like the WPF UI thread, so ordering matches the app.</summary>
internal sealed class SingleThreadContext : SynchronizationContext
{
    private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> queue = new();

    public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

    public static void Run(Func<Task> body)
    {
        var previous = Current;
        var ctx = new SingleThreadContext();
        SetSynchronizationContext(ctx);
        try
        {
            var task = body();
            task.ContinueWith(_ => ctx.queue.CompleteAdding(), TaskScheduler.Default);
            foreach (var (d, state) in ctx.queue.GetConsumingEnumerable()) d(state);
            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
