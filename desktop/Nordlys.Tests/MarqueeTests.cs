using Nordlys.Core;

namespace Nordlys.Tests;

public class MarqueeTests
{
    private static readonly Bounds B = new(720, 520, 1900, 1000);

    [Fact]
    public void Clamps_and_rounds()
    {
        Assert.Equal(new Size2(720, 520), MarqueeGeometry.Clamp(100, 100, B));
        Assert.Equal(new Size2(1900, 1000), MarqueeGeometry.Clamp(5000, 5000, B));
        Assert.Equal(new Size2(1001, 700), MarqueeGeometry.Clamp(1000.6, 700.4, B));
    }

    [Fact]
    public void Keeps_the_grab_offset()
    {
        Assert.Equal(new Size2(1100, 760), MarqueeGeometry.FromPointer(1094, 754, 6, 6, B));
        Assert.Equal(new Size2(1300, 860), MarqueeGeometry.FromPointer(1294, 854, 6, 6, B));
    }

    [Fact]
    public void Arrow_keys()
    {
        Assert.Equal(new Size2(1016, 700), MarqueeGeometry.KeyStep(new(1000, 700), "Right", false, B));
        Assert.Equal(new Size2(1000, 604), MarqueeGeometry.KeyStep(new(1000, 700), "Up", true, B));
        Assert.Equal(new Size2(720, 700), MarqueeGeometry.KeyStep(new(730, 700), "Left", true, B));
        Assert.Null(MarqueeGeometry.KeyStep(new(1000, 700), "Enter", false, B));
    }

    [Fact]
    public void Work_area_bounds_at_100_and_150_percent_and_second_monitor()
    {
        Assert.Equal((1820d, 990d), Pair(MarqueeGeometry.ForWorkArea(100, 50, new Rect2(0, 0, 1920, 1040), 1)));
        Assert.Equal((2360d, 1300d), Pair(MarqueeGeometry.ForWorkArea(300, 150, new Rect2(0, 0, 3840, 2100), 1.5)));
        Assert.Equal((2460d, 1400d), Pair(MarqueeGeometry.ForWorkArea(2020, 0, new Rect2(1920, 0, 2560, 1400), 1)));
        Assert.Equal((720d, 520d), Pair(MarqueeGeometry.ForWorkArea(1800, 900, new Rect2(0, 0, 1920, 1040), 1)));
    }

    private static (double, double) Pair(Bounds b) => (b.MaxW, b.MaxH);
}
