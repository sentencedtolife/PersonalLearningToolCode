// All file access goes through IStore. Paths are relative to the data folder and use "/".
namespace Nordlys.Core.Storage;

public interface IStore
{
    Task<string> ReadTextAsync(string path);
    /// <summary>Atomic: writes a temp file, then replaces the target. Creates parent folders.</summary>
    Task WriteTextAsync(string path, string text);
    /// <summary>File names (not folders) directly inside dir. Empty if dir does not exist.</summary>
    Task<IReadOnlyList<string>> ListAsync(string dir);
    Task<bool> ExistsAsync(string path);
    Task RemoveAsync(string path);
    /// <summary>The folder on disk, or null for in-memory stores.</summary>
    string? Root { get; }
}

/// <summary>The data folder: %APPDATA%\app.nordlys (the same folder the earlier version used).</summary>
public sealed class FileStore(string root) : IStore
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "app.nordlys");

    private readonly HashSet<string> knownDirs = [];

    public string? Root => root;

    private string Full(string path) => Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));

    public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(Full(path));

    public async Task WriteTextAsync(string path, string text)
    {
        var full = Full(path);
        var dir = Path.GetDirectoryName(full)!;
        if (knownDirs.Add(dir)) Directory.CreateDirectory(dir);
        var tmp = full + ".tmp";
        await File.WriteAllTextAsync(tmp, text);
        File.Move(tmp, full, overwrite: true);
    }

    public Task<IReadOnlyList<string>> ListAsync(string dir)
    {
        var full = Full(dir);
        IReadOnlyList<string> names = Directory.Exists(full) ? Directory.GetFiles(full).Select(Path.GetFileName).OfType<string>().Order().ToList() : [];
        return Task.FromResult(names);
    }

    public Task<bool> ExistsAsync(string path) => Task.FromResult(File.Exists(Full(path)) || Directory.Exists(Full(path)));

    public Task RemoveAsync(string path)
    {
        File.Delete(Full(path));
        return Task.CompletedTask;
    }
}

/// <summary>For tests.</summary>
public sealed class MemoryStore : IStore
{
    private readonly Dictionary<string, string> files = [];
    public string? Root => null;

    public Task<string> ReadTextAsync(string path) => files.TryGetValue(path, out var v) ? Task.FromResult(v) : throw new FileNotFoundException(path);
    public Task WriteTextAsync(string path, string text) { files[path] = text; return Task.CompletedTask; }
    public Task<IReadOnlyList<string>> ListAsync(string dir)
    {
        var prefix = dir == "" ? "" : dir + "/";
        IReadOnlyList<string> names = files.Keys.Where(k => k.StartsWith(prefix) && !k[prefix.Length..].Contains('/')).Select(k => k[prefix.Length..]).Order().ToList();
        return Task.FromResult(names);
    }
    public Task<bool> ExistsAsync(string path) => Task.FromResult(files.ContainsKey(path) || files.Keys.Any(k => k.StartsWith(path + "/")));
    public Task RemoveAsync(string path) { files.Remove(path); return Task.CompletedTask; }
}
