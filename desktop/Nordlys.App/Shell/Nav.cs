// Screens and routes. Routes keep the "#/lesson/<id>/practice?x=y" form, so the saved resume route
// in progress.json still works.
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Nordlys.Core;

namespace Nordlys.App.Shell;

public abstract class View
{
    public abstract string Title { get; }
    public abstract FrameworkElement Root { get; }
    /// <summary>Context actions for the header bar (right side).</summary>
    public virtual IEnumerable<UIElement> Actions => [];
    /// <summary>Return true if the key was handled. Called before global shortcuts.</summary>
    public virtual bool OnKey(KeyEventArgs e) => false;
    /// <summary>Called after the view is on screen.</summary>
    public virtual void Shown() { }
    public virtual void Closed() { }
}

public sealed record Route(string Path, IReadOnlyList<string> Parts, IReadOnlyDictionary<string, string> Query)
{
    public static Route Parse(string hash)
    {
        var s = hash.TrimStart('#');
        var q = s.IndexOf('?');
        var path = q >= 0 ? s[..q] : s;
        var query = new Dictionary<string, string>();
        if (q >= 0)
            foreach (var kv in s[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = kv.IndexOf('=');
                if (eq > 0) query[Uri.UnescapeDataString(kv[..eq])] = Uri.UnescapeDataString(kv[(eq + 1)..]);
            }
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToList();
        return new Route(path, parts, query);
    }

    public string Section => Parts.Count > 0 ? Parts[0] : "home";
}

public sealed class Navigator(AppState app, Func<Route, View> resolve)
{
    private readonly List<string> stack = [];
    public View? Current { get; private set; }
    public string CurrentHash => stack.Count > 0 ? stack[^1] : "#/home";
    public event Action<string, View, TimeSpan>? Rendered;

    public void Navigate(string hash, bool replace = false)
    {
        if (replace && stack.Count > 0) stack[^1] = hash;
        else if (stack.Count == 0 || stack[^1] != hash) stack.Add(hash);
        if (stack.Count > 100) stack.RemoveAt(0);
        Render(hash);
    }

    public bool CanGoBack => stack.Count > 1;

    public void Back()
    {
        if (stack.Count > 1)
        {
            stack.RemoveAt(stack.Count - 1);
            Render(stack[^1]);
        }
        else if (CurrentHash != "#/home") Navigate("#/home", replace: true);
    }

    public void Reload() => Render(CurrentHash);

    private void Render(string hash)
    {
        var sw = Stopwatch.StartNew();
        View view;
        try
        {
            view = resolve(Route.Parse(hash));
        }
        catch (Exception e)
        {
            // One broken screen never takes the app down: show the error, log the details, keep navigation working.
            view = new MessageView("Error", $"Something went wrong opening this screen: {e.Message}");
            try { if (app.Store.Root is { } root) System.IO.File.AppendAllText(System.IO.Path.Combine(root, "crash.log"), $"{DateTime.Now:O} opening {hash}\n{e}\n\n"); }
            catch (System.IO.IOException) { }
        }
        Current?.Closed();
        Current = view;
        if (app.Progress.Resume.Route != hash)
        {
            app.Progress.Resume.Route = hash;
            _ = app.SaveProgressAsync();
        }
        Rendered?.Invoke(hash, view, sw.Elapsed);
    }
}

public sealed class MessageView(string title, string message) : View
{
    public override string Title => title;
    public override FrameworkElement Root { get; } = UI.Ui.Page(UI.Ui.Stack(UI.Ui.H1(title), UI.Ui.Muted(message)));
}
