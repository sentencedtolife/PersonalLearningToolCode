// Marquee resizing: drag the grip in the bottom-right corner (or press Alt+F8, or Enter on the grip, and use the
// arrow keys). A translucent Frost-blue rectangle, like the Windows desktop selection rectangle, previews the new
// size on a click-through overlay above the screen, so it can extend past the window's current edges.
// Release or Enter applies it; Esc cancels. Normal edge resizing, Snap and Win+arrows keep working alongside it.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Nordlys.App.UI;
using Nordlys.Core;

namespace Nordlys.App.Shell;

public sealed class Marquee
{
    private readonly MainWindow main;
    private Window? overlay;
    private Rectangle? rect;
    private Border? label;
    private bool active, keyboard;
    private Size2 start, target;
    private Bounds bounds;
    private double offX, offY, scale;
    private Native.RECT win, work;

    /// <summary>A Thumb: WPF's native drag handle, focusable and visible to screen readers.</summary>
    public Thumb Grip { get; }

    public Marquee(MainWindow main, AppState app)
    {
        this.main = main;
        var glyph = new FrameworkElementFactory(typeof(TextBlock));
        glyph.SetValue(TextBlock.TextProperty, Icons.Resize);
        glyph.SetValue(TextBlock.FontSizeProperty, 12.0);
        glyph.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        glyph.SetResourceReference(TextBlock.ForegroundProperty, "TextFaint");
        glyph.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        glyph.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        frame.AppendChild(glyph);
        Grip = new Thumb
        {
            Template = new ControlTemplate(typeof(Thumb)) { VisualTree = frame }, Width = 18, Height = 18,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 2, 2),
            Cursor = Cursors.SizeNWSE, Focusable = true, ToolTip = "Drag to resize the window. Keyboard: Alt+F8, then arrow keys.",
        };
        Grip.FocusVisualStyle = (Style)Application.Current.FindResource("FocusRing");
        System.Windows.Automation.AutomationProperties.SetName(Grip, "Resize window. Press Enter, then use the arrow keys.");
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(Grip, true);
        Grip.DragStarted += (_, _) => Begin(keyboardMode: false);
        Grip.DragDelta += (_, _) => { if (active && !keyboard) Track(); };
        Grip.DragCompleted += (_, e) => { if (active && !keyboard) { if (e.Canceled) Cancel(); else Apply(); } };
        Grip.KeyDown += (_, e) => { if (!active && e.Key is Key.Enter or Key.Space) { e.Handled = true; Begin(keyboardMode: true); } };
        Menus.Attach(Grip, _ =>
        [
            new MenuEntry("Resize with the keyboard", () => Begin(true), Icons.Resize, "Alt+F8"),
            new MenuEntry(main.WindowState == WindowState.Maximized ? "Restore" : "Maximise", () => main.WindowState = main.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized, main.WindowState == WindowState.Maximized ? Icons.Restore : Icons.Maximize),
            new MenuEntry(main.IsFullscreen ? "Exit fullscreen" : "Fullscreen", main.ToggleFullscreen, main.IsFullscreen ? Icons.ExitFullscreen : Icons.Fullscreen, "F11"),
        ]);
    }

    public void SyncGrip(bool hidden) => Grip.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;

    public void BeginKeyboard() => Begin(keyboardMode: true);

    private void Begin(bool keyboardMode)
    {
        if (active) return;
        if (main.IsFullscreen) main.ToggleFullscreen();
        if (main.WindowState == WindowState.Maximized) main.WindowState = WindowState.Normal;
        var hwnd = main.Handle;
        Native.GetWindowRect(hwnd, out win);
        (_, work) = Native.MonitorOf(hwnd);
        scale = Native.Scale(hwnd);
        start = target = new Size2(Math.Round(main.ActualWidth), Math.Round(main.ActualHeight));
        bounds = MarqueeGeometry.ForWorkArea(win.Left, win.Top, new Rect2(work.Left, work.Top, work.Width, work.Height), scale);
        keyboard = keyboardMode;
        if (!keyboard)
        {
            var (x, y) = CursorInWindow();
            offX = start.W - x;
            offY = start.H - y;
        }
        active = true;
        ShowOverlay();
        Draw();
    }

    /// <summary>Cursor position relative to the window's top-left, in DIPs. Works outside the window while captured.</summary>
    private (double X, double Y) CursorInWindow()
    {
        Native.GetCursorPos(out var p);
        return ((p.X - win.Left) / scale, (p.Y - win.Top) / scale);
    }

    private void Track()
    {
        var (x, y) = CursorInWindow();
        target = MarqueeGeometry.FromPointer(x, y, offX, offY, bounds);
        Draw();
    }

    private void ShowOverlay()
    {
        rect = new Rectangle { StrokeThickness = 1, IsHitTestVisible = false };
        rect.Res(Shape.FillProperty, "Marquee").Res(Shape.StrokeProperty, "MarqueeEdge");
        var text = new TextBlock { Foreground = new SolidColorBrush(Theme.Nord.N6), FontSize = 12 };
        text.Res(TextBlock.FontFamilyProperty, "MonoFont");
        label = new Border { Child = text, Background = new SolidColorBrush(Theme.Nord.N0), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), BorderThickness = new Thickness(1), IsHitTestVisible = false };
        label.Res(Border.BorderBrushProperty, "MarqueeEdge");
        var canvas = new Canvas { IsHitTestVisible = false, Children = { rect, label } };
        overlay = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false,
            Topmost = true, ShowActivated = false, ResizeMode = ResizeMode.NoResize, Content = canvas, IsHitTestVisible = false,
            Owner = main, Width = 1, Height = 1, Left = -10000, Top = -10000,
        };
        overlay.SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(overlay).Handle;
            // Click-through and never activated, so the main window keeps focus and the mouse capture.
            var ex = GetWindowLong(h, -20);
            SetWindowLong(h, -20, ex | 0x20 | 0x08000000 | 0x80); // WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            Native.MoveResize(h, work.Left, work.Top, work.Width, work.Height);
        };
        overlay.Show();
    }

    private void Draw()
    {
        if (rect == null || label == null) return;
        // Overlay origin = work-area corner; the window's corner stays where it is.
        var x = (win.Left - work.Left) / scale;
        var y = (win.Top - work.Top) / scale;
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        rect.Width = target.W;
        rect.Height = target.H;
        ((TextBlock)label.Child).Text = $"{target.W:0} × {target.H:0}";
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, x + Math.Max(8, target.W - label.DesiredSize.Width - 8));
        Canvas.SetTop(label, y + Math.Max(8, target.H - label.DesiredSize.Height - 8));
    }

    private void End()
    {
        active = false;
        overlay?.Close();
        overlay = null;
        rect = null;
        label = null;
    }

    private void Apply()
    {
        if (!active) return;
        var t = target;
        End();
        main.Width = t.W;
        main.Height = t.H;
        System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(Grip)?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
    }

    private void Cancel()
    {
        if (!active) return;
        End();
        if (Grip.IsDragging) Grip.CancelDrag();
    }

    /// <summary>While resizing, keys belong to the marquee. Returns true if handled.</summary>
    public bool OnKey(KeyEventArgs e)
    {
        if (!active) return false;
        if (e.Key == Key.Escape) { Cancel(); return true; }
        if (!keyboard) return true; // swallow keys during a mouse drag
        if (e.Key == Key.Enter) { Apply(); return true; }
        var dir = e.Key switch { Key.Left => "Left", Key.Right => "Right", Key.Up => "Up", Key.Down => "Down", _ => null };
        if (dir != null && MarqueeGeometry.KeyStep(target, dir, (Keyboard.Modifiers & ModifierKeys.Shift) != 0, bounds) is { } next)
        {
            target = next;
            Draw();
        }
        return true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLong(nint h, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLong(nint h, int index, nint value);
}
