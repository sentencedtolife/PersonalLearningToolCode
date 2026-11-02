// Nord palette and design tokens as application resources. Everything uses DynamicResource,
// so theme, accent, density and font size switch live without rebuilding the UI.
using System.Windows;
using System.Windows.Media;
using Nordlys.Core;

namespace Nordlys.App.Theme;

public static class Nord
{
    // Polar Night, shifted darker and toward steel gray (less blue than Nord's #2E3440…#4C566A).
    public static readonly Color N0 = C("#262A30"), N1 = C("#30353C"), N2 = C("#3A4048"), N3 = C("#4A515B");
    public static readonly Color N4 = C("#D8DEE9"), N5 = C("#E5E9F0"), N6 = C("#ECEFF4");
    public static readonly Color N7 = C("#8FBCBB"), N8 = C("#88C0D0"), N9 = C("#81A1C1"), N10 = C("#5E81AC");
    public static readonly Color N11 = C("#BF616A"), N12 = C("#D08770"), N13 = C("#EBCB8B"), N14 = C("#A3BE8C"), N15 = C("#B48EAD");

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static readonly Dictionary<string, Color> Tracks = new()
    {
        ["C"] = N8, ["X"] = N13, ["P"] = N10, ["D"] = N15, ["A"] = N12, ["S"] = N14, ["N"] = N7, ["K"] = N9,
    };

    public static readonly Color[] Mastery = [N3, N9, N8, N14];

    public static readonly Dictionary<string, Color> Accents = new()
    {
        ["frost"] = N8, ["teal"] = N7, ["blue"] = N9, ["deep"] = N10, ["green"] = N14, ["purple"] = N15, ["orange"] = N12,
    };

    public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R * (1 - t) + b.R * t), (byte)Math.Round(a.G * (1 - t) + b.G * t), (byte)Math.Round(a.B * (1 - t) + b.B * t));
}

public static class ThemeManager
{
    public static bool Dark { get; private set; } = true;
    public static event Action? Changed;

    private static void Brush(ResourceDictionary r, string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        r[key] = b;
    }

    public static void Apply(Settings s)
    {
        var r = Application.Current.Resources;
        Dark = s.Theme != "light";
        var accentBase = Nord.Accents.GetValueOrDefault(s.Accent, Nord.N8);

        if (Dark)
        {
            Brush(r, "Bg", Nord.N0);
            Brush(r, "Surface", Nord.N1);
            Brush(r, "Surface2", Nord.N2);
            Brush(r, "Border", Nord.N2);
            Brush(r, "Text", Nord.N6);
            Brush(r, "TextMuted", Nord.N4);
            Brush(r, "TextFaint", Nord.C("#8C949E"));
            Brush(r, "DoneBox", Nord.N2);
            Brush(r, "CodeBg", Nord.C("#1F2328"));
            Brush(r, "Scrollbar", Nord.N3);
            Brush(r, "Correct", Nord.N14);
            Brush(r, "NotYet", Nord.N13);
            Brush(r, "Accent", accentBase);
            Brush(r, "AccentText", s.Accent == "deep" ? Nord.N6 : Nord.N0);
        }
        else
        {
            Brush(r, "Bg", Nord.N6);
            Brush(r, "Surface", Nord.N5);
            Brush(r, "Surface2", Nord.N4);
            Brush(r, "Border", Nord.N4);
            Brush(r, "Text", Nord.N0);
            Brush(r, "TextMuted", Nord.N3);
            Brush(r, "TextFaint", Nord.C("#6B7487"));
            Brush(r, "DoneBox", Nord.N4);
            Brush(r, "CodeBg", Nord.C("#E2E7EF"));
            Brush(r, "Scrollbar", Nord.C("#C4CBD8"));
            // Yellow and green need more contrast on Snow Storm backgrounds.
            Brush(r, "Correct", Nord.C("#5F8A45"));
            Brush(r, "NotYet", Nord.C("#B88A1E"));
            // Pastel accents are darkened for contrast; the default stays nord10.
            Brush(r, "Accent", s.Accent == "frost" ? Nord.N10 : Nord.Mix(accentBase, Nord.N0, 0.4));
            Brush(r, "AccentText", Nord.N6);
        }
        // Code highlighting.
        Brush(r, "HlKeyword", Dark ? Nord.N9 : Nord.N10);
        Brush(r, "HlString", Dark ? Nord.N14 : Nord.C("#5F8A45"));
        Brush(r, "HlNumber", Dark ? Nord.N15 : Nord.C("#8C5F86"));
        Brush(r, "HlComment", Dark ? Nord.C("#8C949E") : Nord.C("#6B7487"));
        Brush(r, "HlTitle", Dark ? Nord.N8 : Nord.C("#3B7A8A"));
        Brush(r, "HlMeta", Nord.N12);
        Brush(r, "HlType", Dark ? Nord.N7 : Nord.C("#4C8584"));
        // Button hover / press tints: light over dark surfaces, dark over light ones.
        Brush(r, "HoverTint", Dark ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x0F, 0x26, 0x2A, 0x30));
        Brush(r, "PressTint", Dark ? Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1C, 0x26, 0x2A, 0x30));
        // Hover fades play unless Reduce motion is on or Windows' animation effects are off.
        if (Application.Current.MainWindow is { } w) UI.Motion.SetEnabled(w, !s.ReduceMotion && SystemParameters.ClientAreaAnimation);
        Brush(r, "Error", Nord.N11);
        Brush(r, "Focus", Nord.N8);
        Brush(r, "CloseHover", Nord.N11);
        Brush(r, "Marquee", Color.FromArgb((byte)Math.Round(255 * s.MarqueeAlpha), Nord.N9.R, Nord.N9.G, Nord.N9.B));
        Brush(r, "MarqueeEdge", Color.FromArgb(242, Nord.N9.R, Nord.N9.G, Nord.N9.B));
        foreach (var (code, c) in Nord.Tracks) Brush(r, $"Track{code}", c);
        for (var i = 0; i < Nord.Mastery.Length; i++) Brush(r, $"Mastery{i}", Nord.Mastery[i]);

        // Type scale.
        var baseSize = s.FontSize switch { "S" => 13.0, "L" => 17.0, _ => 15.0 };
        r["FontBase"] = baseSize;
        r["FontSmall"] = Math.Round(baseSize * 0.87, 1);
        r["FontTiny"] = Math.Round(baseSize * 0.78, 1);
        r["FontH1"] = Math.Round(baseSize * 1.75);
        r["FontH2"] = Math.Round(baseSize * 1.3);
        r["FontH3"] = Math.Round(baseSize * 1.1);
        r["FontBig"] = Math.Round(baseSize * 1.6);
        r["PaperFont"] = new FontFamily(s.PaperFont == "serif" ? "Cambria, Georgia" : "Segoe UI Variable Text, Segoe UI");

        // Density tokens.
        var compact = s.Density == "compact";
        r["PadPage"] = compact ? new Thickness(16, 20, 16, 64) : new Thickness(24, 32, 24, 80);
        r["PadCard"] = compact ? new Thickness(12) : new Thickness(16);
        r["PadQ"] = compact ? new Thickness(14) : new Thickness(20);
        r["PadBtn"] = compact ? new Thickness(12, 4, 12, 4) : new Thickness(16, 7, 16, 7);
        r["PadChoice"] = compact ? new Thickness(10, 6, 10, 6) : new Thickness(12, 10, 12, 10);
        r["ControlHeight"] = compact ? 32.0 : 38.0;
        r["NavHeight"] = compact ? 32.0 : 38.0;
        r["Gap"] = compact ? 10.0 : 16.0;
        r["PadSetting"] = compact ? new Thickness(0, 9, 0, 9) : new Thickness(0, 14, 0, 14);

        Changed?.Invoke();
    }

    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
