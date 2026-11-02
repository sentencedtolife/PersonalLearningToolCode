// Small builders so screens read like their layout. Colours and sizes always come from theme resources.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Nordlys.App.UI;

/// <summary>Segoe Fluent Icons code points.</summary>
public static class Icons
{
    public const string Home = "", Library = "", Map = "", Review = "", Ask = "", Import = "",
        Glossary = "", Settings = "", Back = "", Menu = "", Up = "", Down = "",
        Minimize = "", Maximize = "", Restore = "", Close = "", Copy = "", Cut = "", Paste = "",
        SelectAll = "", Search = "", Delete = "", Edit = "", Open = "", Folder = "", Refresh = "",
        Play = "", Hint = "", Show = "", Info = "", Link = "", Page = "", Clear = "",
        Duplicate = "", Fullscreen = "", ExitFullscreen = "", Resize = "", Reset = "", Undo = "", Redo = "",
        Check = "";
}

/// <summary>
/// A focusable item that screen readers and UI Automation see as a list item with a name.
/// (Plain panels are invisible to assistive technology.)
/// </summary>
public sealed class AccessibleItem : ContentControl
{
    public AccessibleItem()
    {
        Focusable = true;
        IsTabStop = true;
        FocusVisualStyle = (Style)Application.Current.FindResource("FocusRing");
        Background = Brushes.Transparent;
        Template = new ControlTemplate(typeof(AccessibleItem)) { VisualTree = Presenter() };
    }

    private static FrameworkElementFactory Presenter()
    {
        var b = new FrameworkElementFactory(typeof(Border));
        b.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        b.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        b.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        return b;
    }

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(AccessibleItem owner) : System.Windows.Automation.Peers.FrameworkElementAutomationPeer(owner)
    {
        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => System.Windows.Automation.Peers.AutomationControlType.ListItem;
        protected override string GetClassNameCore() => nameof(AccessibleItem);
        protected override bool IsKeyboardFocusableCore() => true;
    }
}

public static class TaskExtensions
{
    /// <summary>Runs a task without awaiting it; failures are logged instead of lost.</summary>
    public static void Forget(this Task t) => t.ContinueWith(x => System.Diagnostics.Debug.WriteLine(x.Exception), TaskContinuationOptions.OnlyOnFaulted);
}

public static class Ui
{
    public static T Res<T>(this T el, DependencyProperty p, string key) where T : FrameworkElement
    {
        el.SetResourceReference(p, key);
        return el;
    }

    public static T ResC<T>(this T el, DependencyProperty p, string key) where T : FrameworkContentElement
    {
        el.SetResourceReference(p, key);
        return el;
    }

    public static TextBlock Text(string s, string size = "FontBase", string brush = "Text", FontWeight? weight = null)
    {
        var t = new TextBlock { Text = s, FontWeight = weight ?? FontWeights.Normal };
        t.Res(TextBlock.FontSizeProperty, size).Res(TextBlock.ForegroundProperty, brush).Res(TextBlock.FontFamilyProperty, "UiFont");
        return t;
    }

    public static TextBlock H1(string s) => With(Text(s, "FontH1", "Text", FontWeights.SemiBold), t => t.Margin = new Thickness(0, 0, 0, 12));
    public static TextBlock H2(string s) => With(Text(s, "FontH2", "Text", FontWeights.SemiBold), t => t.Margin = new Thickness(0, 8, 0, 8));
    public static TextBlock Muted(string s, string size = "FontBase") => Text(s, size, "TextMuted");

    public static TextBlock Icon(string glyph, double size = 16, string brush = "TextMuted")
    {
        var t = new TextBlock { Text = glyph, FontSize = size, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        return t.Res(TextBlock.FontFamilyProperty, "IconFont").Res(TextBlock.ForegroundProperty, brush);
    }

    public static T With<T>(T x, Action<T> f) { f(x); return x; }

    /// <summary>A button. key = the keyboard shortcut shown as a small badge (e.g. "Enter").</summary>
    public static Button Btn(string label, Action onClick, string style = "Btn", string? key = null, bool big = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        if (big) { text.Res(TextBlock.FontSizeProperty, "FontBig"); text.FontWeight = FontWeights.SemiBold; }
        content.Children.Add(text);
        if (key != null) content.Children.Add(KeyBadge(key));
        var b = new Button { Content = content };
        b.Res(FrameworkElement.StyleProperty, style);
        if (big) b.Padding = new Thickness(64, 18, 64, 18);
        b.Click += (_, _) => onClick();
        System.Windows.Automation.AutomationProperties.SetName(b, label);
        return b;
    }

    public static Border KeyBadge(string key)
    {
        var t = new TextBlock { Text = key, FontSize = 11, Margin = new Thickness(5, 0, 5, 0), TextWrapping = TextWrapping.NoWrap };
        t.Res(TextBlock.FontFamilyProperty, "MonoFont");
        var b = new Border { Child = t, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(8, 0, 0, 0), Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
        b.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Control), 1) });
        t.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Control), 1) });
        return b;
    }

    public static Button IconBtn(string glyph, string label, Action onClick)
    {
        var b = new Button { Content = glyph, ToolTip = label };
        b.Res(FrameworkElement.StyleProperty, "IconBtn");
        System.Windows.Automation.AutomationProperties.SetName(b, label);
        b.Click += (_, _) => onClick();
        return b;
    }

    public static StackPanel Stack(params UIElement?[] children) => StackGap("Gap", children);

    public static StackPanel StackGap(string gapKey, params UIElement?[] children)
    {
        var s = new StackPanel();
        var gap = Application.Current.TryFindResource(gapKey) is double d ? d : 16;
        foreach (var c in children.OfType<UIElement>())
        {
            if (s.Children.Count > 0 && c is FrameworkElement fe && fe.Margin.Top == 0) fe.Margin = new Thickness(fe.Margin.Left, gap, fe.Margin.Right, fe.Margin.Bottom);
            s.Children.Add(c);
        }
        return s;
    }

    public static WrapPanel Row(params UIElement?[] children)
    {
        var w = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var c in children.OfType<UIElement>())
        {
            if (c is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 8, 8);
            w.Children.Add(c);
        }
        return w;
    }

    public static Border Card(UIElement child, string pad = "PadCard")
    {
        var b = new Border { Child = child, BorderThickness = new Thickness(1) };
        b.Res(Border.BackgroundProperty, "Surface").Res(Border.BorderBrushProperty, "Border").Res(Border.PaddingProperty, pad);
        b.SetResourceReference(Border.CornerRadiusProperty, "Radius");
        return b;
    }

    public static Border Chip(string text, string? bgKey = null, string fg = "TextMuted")
    {
        var t = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.NoWrap };
        t.Res(TextBlock.ForegroundProperty, bgKey != null ? "Bg" : fg);
        if (bgKey != null) t.FontWeight = FontWeights.SemiBold;
        var b = new Border { Child = t, CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2, 8, 2), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 6) };
        if (bgKey != null) b.Res(Border.BackgroundProperty, bgKey).Res(Border.BorderBrushProperty, bgKey);
        else b.Res(Border.BorderBrushProperty, "Border");
        return b;
    }

    public static Border DoneBox(string doneMeans)
    {
        var label = Text("DONE MEANS", "FontTiny", "TextMuted", FontWeights.SemiBold);
        label.Margin = new Thickness(0, 0, 0, 2);
        var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12), Child = new StackPanel { Children = { label, Text(doneMeans) } } };
        return b.Res(Border.BackgroundProperty, "DoneBox");
    }

    /// <summary>Segmented toggle (e.g. Paper / Practice, Dark / Light).</summary>
    public static Border Segmented(params (string Label, bool Active, Action OnClick)[] options)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var group = Guid.NewGuid().ToString("N");
        foreach (var (label, active, onClick) in options)
        {
            // Radio buttons: one choice, arrow keys move between them, screen readers announce "selected".
            var t = new RadioButton { Content = label, IsChecked = active, GroupName = group };
            t.Res(FrameworkElement.StyleProperty, "SegItem");
            t.Checked += (_, _) => onClick();
            System.Windows.Automation.AutomationProperties.SetName(t, label);
            panel.Children.Add(t);
        }
        var b = new Border { Child = panel, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        return b.Res(Border.BorderBrushProperty, "Border");
    }

    public static Border Swatch(Color c, double size = 12) => new() { Width = size, Height = size, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(c), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>A page: centred column with the density padding, inside a scroll viewer.</summary>
    public static ScrollViewer Page(UIElement content, double maxWidth = 880)
    {
        var inner = new Border { Child = content, MaxWidth = maxWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        inner.Res(Border.PaddingProperty, "PadPage");
        var outer = new Grid();
        outer.Children.Add(inner);
        inner.HorizontalAlignment = HorizontalAlignment.Center;
        inner.Width = double.NaN;
        // Transparent background: empty areas still receive right-clicks.
        var sv = new ScrollViewer { Content = outer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Background = Brushes.Transparent };
        // Keep the column centred but let it shrink with the window.
        outer.SizeChanged += (_, e) => inner.Width = Math.Min(maxWidth, e.NewSize.Width);
        return sv;
    }

    public static Border Notice(string text)
    {
        var b = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Child = Text(text, "FontSmall") };
        return b.Res(Border.BorderBrushProperty, "NotYet");
    }
}
