// Pure geometry for marquee resizing. Sizes are device-independent pixels (DIPs) unless named *Phys.
namespace Nordlys.Core;

public readonly record struct Size2(double W, double H);
public readonly record struct Bounds(double MinW, double MinH, double MaxW, double MaxH);
public readonly record struct Rect2(double X, double Y, double W, double H);

public static class MarqueeGeometry
{
    public static readonly Size2 MinSize = new(720, 520);
    public const double Step = 16;
    public const double BigStep = 96;

    public static Size2 Clamp(double w, double h, Bounds b) => new(
        Math.Round(Math.Min(Math.Max(w, b.MinW), Math.Max(b.MinW, b.MaxW))),
        Math.Round(Math.Min(Math.Max(h, b.MinH), Math.Max(b.MinH, b.MaxH))));

    /// <summary>
    /// The window's top-left corner stays put; the bottom-right corner follows the pointer.
    /// off keeps the grab point where it was, so pressing the grip doesn't make the size jump.
    /// </summary>
    public static Size2 FromPointer(double x, double y, double offX, double offY, Bounds b) => Clamp(x + offX, y + offY, b);

    /// <summary>Arrow keys: Right / Left change the width, Down / Up the height. Shift = bigger steps.</summary>
    public static Size2? KeyStep(Size2 s, string key, bool shift, Bounds b)
    {
        var d = shift ? BigStep : Step;
        return key switch
        {
            "Right" => Clamp(s.W + d, s.H, b),
            "Left" => Clamp(s.W - d, s.H, b),
            "Down" => Clamp(s.W, s.H + d, b),
            "Up" => Clamp(s.W, s.H - d, b),
            _ => null,
        };
    }

    /// <summary>
    /// The largest size that keeps the window inside its monitor's work area (the screen minus the taskbar).
    /// Computed in physical pixels, then converted with the window's scale, so it is right on high-DPI and mixed-DPI setups.
    /// </summary>
    public static Bounds ForWorkArea(double windowXPhys, double windowYPhys, Rect2? workAreaPhys, double scale, Size2? min = null)
    {
        var m = min ?? MinSize;
        if (workAreaPhys is not { } wa) return new Bounds(m.W, m.H, double.PositiveInfinity, double.PositiveInfinity);
        var maxW = (wa.X + wa.W - windowXPhys) / scale;
        var maxH = (wa.Y + wa.H - windowYPhys) / scale;
        return new Bounds(m.W, m.H, Math.Max(m.W, Math.Floor(maxW)), Math.Max(m.H, Math.Floor(maxH)));
    }
}
