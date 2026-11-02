// settings.json. Same field names as the earlier version, plus window placement.
using System.Text.Json;

namespace Nordlys.Core;

public sealed class Settings
{
    public static readonly string[] Accents = ["frost", "teal", "blue", "deep", "green", "purple", "orange"];

    public int Version { get; set; } = 1;
    public string Theme { get; set; } = "dark";          // dark | light
    public string Accent { get; set; } = "frost";
    public string Density { get; set; } = "comfortable"; // comfortable | compact
    /// <summary>Fill opacity of the marquee-resize preview.</summary>
    public double MarqueeAlpha { get; set; } = 0.25;
    public string PaperFont { get; set; } = "sans";      // sans | serif
    public string FontSize { get; set; } = "M";          // S | M | L
    /// <summary>True = no animations. Windows' "Show animations" setting also turns them off.</summary>
    public bool ReduceMotion { get; set; }
    public bool SidebarCollapsed { get; set; }
    public int LastEnergy { get; set; } = 2;
    /// <summary>Normal (restored) window bounds in device-independent pixels, and whether it was maximised.</summary>
    public WindowBounds? Window { get; set; }

    public static Settings Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Settings();
        try { return JsonSerializer.Deserialize<Settings>(text, Progress.ProgressStore.Json) ?? new Settings(); }
        catch (JsonException) { return new Settings(); }
    }

    public string Serialize() => JsonSerializer.Serialize(this, Progress.ProgressStore.Json);
}

public sealed record WindowBounds(double Left, double Top, double Width, double Height, bool Maximized);
