// The main window: Nord header bar (WindowChrome keeps native snapping, the system menu on right-click
// and Alt+Space, Aero Shake, and Snap Layouts on the maximise button), sidebar, and the current screen.
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Nordlys.App.Shell;
using Nordlys.App.Theme;
using Nordlys.App.UI;
using Nordlys.App.Views;
using Nordlys.Core;

namespace Nordlys.App;

public sealed class MainWindow : Window
{
    private readonly AppState app;
    public Navigator Nav { get; }
    private readonly TextBlock title = Ui.Text("Nordlys", "FontBase", "Text", FontWeights.SemiBold);
    private readonly StackPanel actions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button back;
    private readonly Button maxBtn;
    private readonly Border content = new() { ClipToBounds = true };
    private readonly Border sidebar = new() { BorderThickness = new Thickness(0, 0, 1, 0) };
    private readonly List<(string Route, Button Button, TextBlock Label, TextBlock Key)> navItems = [];
    private readonly Marquee marquee;
    private nint hwnd;
    private bool fullscreen;
    private Native.WINDOWPLACEMENT beforeFullscreen;
    private bool closingSaved;
    private DateTime gPressed = DateTime.MinValue;

    public MainWindow(AppState app)
    {
        this.app = app;
        Title = "Nordlys";
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/nordlys.ico"));
        Width = 1100;
        Height = 760;
        MinWidth = MarqueeGeometry.MinSize.W;
        MinHeight = MarqueeGeometry.MinSize.H;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        this.Res(BackgroundProperty, "Bg").Res(FontFamilyProperty, "UiFont").Res(FontSizeProperty, "FontBase").Res(ForegroundProperty, "Text");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        Motion.SetEnabled(this, !app.Settings.ReduceMotion && SystemParameters.ClientAreaAnimation);

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 46,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0, 0, 0, 1), // keeps the DWM shadow
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        Nav = new Navigator(app, Routes.Resolve);
        Nav.Rendered += OnRendered;

        // Header bar
        back = Ui.IconBtn(Icons.Back, "Back (Esc)", () => Nav.Back());
        var appIcon = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/nordlys.png")), Width = 20, Height = 20, Margin = new Thickness(6, 0, 6, 0) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { back, appIcon } };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;
        var min = CaptionButton(Icons.Minimize, "Minimise", () => WindowState = WindowState.Minimized);
        maxBtn = CaptionButton(Icons.Maximize, "Maximise", ToggleMaximize);
        var close = CaptionButton(Icons.Close, "Close", Close, "WinClose");
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { actions, min, maxBtn, close } };
        foreach (var el in new FrameworkElement[] { back, actions }) WindowChrome.SetIsHitTestVisibleInChrome(el, true);

        var header = new Grid { Height = 46 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(title, 1);
        Grid.SetColumn(right, 2);
        header.Children.Add(left);
        header.Children.Add(title);
        header.Children.Add(right);
        var headerBorder = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) };
        headerBorder.Res(Border.BackgroundProperty, "Surface").Res(Border.BorderBrushProperty, "Border");

        // Sidebar
        var nav = new StackPanel { Margin = new Thickness(8) };
        foreach (var (route, label, glyph, key) in new[]
        {
            ("#/home", "Home", Icons.Home, "G H"), ("#/library", "Library", Icons.Library, "G L"), ("#/map", "Map", Icons.Map, "G M"),
            ("#/review", "Review", Icons.Review, "G R"), ("#/ask", "Ask", Icons.Ask, "G A"), ("#/import", "Import", Icons.Import, "G I"),
            ("#/glossary", "Glossary", Icons.Glossary, ""), ("#/settings", "Settings", Icons.Settings, ""),
        })
        {
            var labelText = Ui.Text(label, "FontBase", "TextMuted");
            labelText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            labelText.TextWrapping = TextWrapping.NoWrap;
            var keyText = Ui.Text(key, "FontTiny", "TextFaint");
            keyText.Res(TextBlock.FontFamilyProperty, "MonoFont");
            keyText.VerticalAlignment = VerticalAlignment.Center;
            var iconText = Ui.Icon(glyph, 16);
            iconText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(iconText, Dock.Left);
            DockPanel.SetDock(keyText, Dock.Right);
            iconText.Margin = new Thickness(0, 0, 12, 0);
            row.Children.Add(iconText);
            row.Children.Add(keyText);
            row.Children.Add(labelText);
            var b = new Button { Content = row, ToolTip = label, Margin = new Thickness(0, 0, 0, 2) };
            b.Res(StyleProperty, "NavItem");
            System.Windows.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => Nav.Navigate(route);
            var r = route;
            Menus.Attach(b, _ => [new MenuEntry($"Open {label}", () => Nav.Navigate(r), Icons.Open), null, new MenuEntry(app.Settings.SidebarCollapsed ? "Expand sidebar" : "Collapse sidebar", ToggleSidebar, Icons.Menu)]);
            navItems.Add((route, b, labelText, keyText));
            nav.Children.Add(b);
        }
        var collapse = new Button { Content = Ui.With(new DockPanel(), d => { var i = Ui.Icon(Icons.Menu, 16); i.Margin = new Thickness(0, 0, 12, 0); DockPanel.SetDock(i, Dock.Left); d.Children.Add(i); d.Children.Add(Ui.Text("Collapse sidebar", "FontBase", "TextMuted")); }), Margin = new Thickness(8) };
        collapse.Res(StyleProperty, "NavItem");
        collapse.Click += (_, _) => ToggleSidebar();
        collapse.VerticalAlignment = VerticalAlignment.Bottom;
        System.Windows.Automation.AutomationProperties.SetName(collapse, "Collapse sidebar");
        var side = new DockPanel();
        DockPanel.SetDock(collapse, Dock.Bottom);
        side.Children.Add(collapse);
        side.Children.Add(nav);
        sidebar.Child = side;
        sidebar.Res(Border.BackgroundProperty, "Surface").Res(Border.BorderBrushProperty, "Border");
        ApplySidebar();

        // Layout: header / [sidebar | content], plus the marquee grip on top.
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumnSpan(headerBorder, 2);
        Grid.SetRow(sidebar, 1);
        Grid.SetRow(content, 1);
        Grid.SetColumn(content, 1);
        root.Children.Add(headerBorder);
        root.Children.Add(sidebar);
        root.Children.Add(content);
        marquee = new Marquee(this, app);
        Grid.SetRowSpan(marquee.Grip, 2);
        Grid.SetColumnSpan(marquee.Grip, 2);
        root.Children.Add(marquee.Grip);
        Content = root;

        SourceInitialized += (_, _) => OnSourceInitialized();
        StateChanged += (_, _) => SyncMaxButton();
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
        ThemeManager.Changed += () => { if (hwnd != 0) Native.UseDarkMode(hwnd, ThemeManager.Dark); };
        Loaded += (_, _) =>
        {
            // Reopen exactly where the app was left.
            var resume = app.Progress.Resume.Route;
            Nav.Navigate(string.IsNullOrEmpty(resume) ? "#/home" : resume);
            App.StartupMs = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        };
    }

    private Button CaptionButton(string glyph, string label, Action onClick, string style = "WinBtn")
    {
        var b = new Button { Content = glyph, ToolTip = label };
        b.Res(StyleProperty, style);
        System.Windows.Automation.AutomationProperties.SetName(b, label);
        b.Click += (_, _) => onClick();
        WindowChrome.SetIsHitTestVisibleInChrome(b, true);
        return b;
    }

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void SyncMaxButton()
    {
        var max = WindowState == WindowState.Maximized;
        maxBtn.Content = max ? Icons.Restore : Icons.Maximize;
        maxBtn.ToolTip = max ? "Restore" : "Maximise";
        System.Windows.Automation.AutomationProperties.SetName(maxBtn, (string)maxBtn.ToolTip);
        marquee.SyncGrip(max || fullscreen);
    }

    private void ToggleSidebar()
    {
        app.Settings.SidebarCollapsed = !app.Settings.SidebarCollapsed;
        _ = app.SaveSettingsAsync();
        ApplySidebar();
    }

    private void ApplySidebar()
    {
        var c = app.Settings.SidebarCollapsed;
        sidebar.Width = c ? 56 : 200;
        foreach (var (_, _, label, key) in navItems) { label.Visibility = c ? Visibility.Collapsed : Visibility.Visible; key.Visibility = label.Visibility; }
    }

    private void OnRendered(string hash, View view, TimeSpan elapsed)
    {
        title.Text = view.Title;
        Title = view.Title == "Nordlys" ? "Nordlys" : $"{view.Title} · Nordlys";
        actions.Children.Clear();
        foreach (var a in view.Actions) { if (a is FrameworkElement fe) WindowChrome.SetIsHitTestVisibleInChrome(fe, true); actions.Children.Add(a); }
        back.Visibility = hash == "#/home" ? Visibility.Hidden : Visibility.Visible;
        var section = "#/" + Route.Parse(hash).Section;
        foreach (var (route, b, _, _) in navItems) b.Tag = route == section ? "current" : null;

        content.Child = view.Root;
        if (!app.Settings.ReduceMotion && SystemParameters.ClientAreaAnimation)
        {
            // 120 ms fade + 4 px rise; opacity and transform only, so it is composited on the GPU.
            var t = new TranslateTransform(0, 4);
            view.Root.RenderTransform = t;
            view.Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        }
        Dispatcher.BeginInvoke(view.Shown, System.Windows.Threading.DispatcherPriority.Loaded);
        Debug.WriteLine($"route {hash}: {elapsed.TotalMilliseconds:0.0} ms");
    }

    // ---------- Win32: placement, Snap Layouts, work area ----------

    private void OnSourceInitialized()
    {
        hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        Native.UseRoundedCorners(hwnd);
        Native.UseDarkMode(hwnd, ThemeManager.Dark);
        if (app.Settings.Window is { } w)
        {
            var rc = new Native.RECT { Left = (int)w.Left, Top = (int)w.Top, Right = (int)(w.Left + w.Width), Bottom = (int)(w.Top + w.Height) };
            if (w.Width >= 200 && w.Height >= 200 && Native.IsOnAnyMonitor(rc))
            {
                var wp = new Native.WINDOWPLACEMENT { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.WINDOWPLACEMENT>(), ShowCmd = w.Maximized ? Native.SW_SHOWMAXIMIZED : Native.SW_SHOWNORMAL, NormalPosition = rc };
                Native.SetWindowPlacement(hwnd, ref wp);
            }
        }
        SyncMaxButton();
    }

    private nint WndProc(nint h, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case Native.WM_GETMINMAXINFO when !fullscreen:
                Native.FitMaximizedToWorkArea(h, lParam);
                break;
            case Native.WM_NCHITTEST when OverMaxButton(lParam):
                // Telling Windows the pointer is on the maximise button shows the Snap Layouts flyout (Windows 11).
                maxBtn.Tag = "hover";
                handled = true;
                return Native.HTMAXBUTTON;
            case Native.WM_NCHITTEST:
                maxBtn.Tag = null;
                break;
            case Native.WM_NCLBUTTONDOWN when wParam == Native.HTMAXBUTTON:
                handled = true;
                break;
            case Native.WM_NCLBUTTONUP when wParam == Native.HTMAXBUTTON:
                handled = true;
                ToggleMaximize();
                break;
            case Native.WM_NCMOUSELEAVE:
                maxBtn.Tag = null;
                break;
        }
        return 0;
    }

    private bool OverMaxButton(nint lParam)
    {
        if (!maxBtn.IsVisible) return false;
        var (x, y) = Native.PointFromLParam(lParam);
        var topLeft = maxBtn.PointToScreen(new Point(0, 0));
        var bottomRight = maxBtn.PointToScreen(new Point(maxBtn.ActualWidth, maxBtn.ActualHeight));
        return x >= topLeft.X && x < bottomRight.X && y >= topLeft.Y && y < bottomRight.Y;
    }

    public void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            beforeFullscreen = new Native.WINDOWPLACEMENT { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.WINDOWPLACEMENT>() };
            Native.GetWindowPlacement(hwnd, ref beforeFullscreen);
            fullscreen = true;
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
            var (monitor, _) = Native.MonitorOf(hwnd);
            Native.MoveResize(hwnd, monitor.Left, monitor.Top, monitor.Width, monitor.Height);
        }
        else
        {
            fullscreen = false;
            Native.SetWindowPlacement(hwnd, ref beforeFullscreen);
        }
        SyncMaxButton();
    }

    public bool IsFullscreen => fullscreen;
    public nint Handle => hwnd;

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (closingSaved) return;
        e.Cancel = true;
        // Remember where the window was (the normal size even when maximised), then make sure progress is on disk.
        var wp = fullscreen ? beforeFullscreen : new Native.WINDOWPLACEMENT { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.WINDOWPLACEMENT>() };
        if (!fullscreen) Native.GetWindowPlacement(hwnd, ref wp);
        var rc = wp.NormalPosition;
        app.Settings.Window = new WindowBounds(rc.Left, rc.Top, rc.Width, rc.Height, wp.ShowCmd == Native.SW_SHOWMAXIMIZED);
        Nav.Current?.Closed();
        try
        {
            await app.SaveSettingsAsync();
            await app.SaveProgressAsync();
        }
        catch (System.IO.IOException) { }
        closingSaved = true;
        Close();
    }

    // ---------- Keyboard ----------

    private static bool IsTyping(object? focused) => focused switch
    {
        TextBox t => !t.IsReadOnly,
        ComboBox => true,
        _ => false,
    };

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        if (marquee.OnKey(e)) { e.Handled = true; return; }
        if (key == Key.F11) { ToggleFullscreen(); e.Handled = true; return; }
        if (key == Key.F8 && mods == ModifierKeys.Alt) { marquee.BeginKeyboard(); e.Handled = true; return; }

        var typing = IsTyping(Keyboard.FocusedElement);

        // "G then X" chords.
        if (gPressed > DateTime.Now.AddMilliseconds(-1500) && !typing && mods == ModifierKeys.None)
        {
            gPressed = DateTime.MinValue;
            var target = key switch { Key.H => "#/home", Key.L => "#/library", Key.M => "#/map", Key.R => "#/review", Key.A => "#/ask", Key.I => "#/import", _ => null };
            if (target != null) { Nav.Navigate(target); e.Handled = true; return; }
        }

        if (typing && key != Key.Escape && !(key == Key.Enter && (mods & ModifierKeys.Control) != 0))
        {
            // Views still see Enter in single-line answer boxes.
            if (!(key == Key.Enter && Keyboard.FocusedElement is TextBox { AcceptsReturn: false })) return;
        }

        if (Nav.Current?.OnKey(e) == true) { e.Handled = true; return; }

        if (key == Key.Escape)
        {
            if (typing) { Keyboard.ClearFocus(); content.Focus(); e.Handled = true; return; }
            if (Nav.CurrentHash != "#/home") { Nav.Back(); e.Handled = true; }
            return;
        }
        if (!typing && key == Key.G && mods == ModifierKeys.None) gPressed = DateTime.Now;
    }
}
