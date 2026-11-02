// Win32 calls the WPF API doesn't cover: monitor work areas, window placement, Snap Layouts, rounded corners.
using System.Runtime.InteropServices;

namespace Nordlys.App.Shell;

internal static partial class Native
{
    public const int WM_GETMINMAXINFO = 0x0024, WM_NCHITTEST = 0x0084, WM_NCMOUSEMOVE = 0x00A0, WM_NCLBUTTONDOWN = 0x00A1,
        WM_NCLBUTTONUP = 0x00A2, WM_NCMOUSELEAVE = 0x02A2, HTMAXBUTTON = 9;
    public const int SW_SHOWNORMAL = 1, SW_SHOWMAXIMIZED = 3;
    private const uint MONITOR_DEFAULTTONULL = 0, MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public readonly int Width => Right - Left; public readonly int Height => Bottom - Top; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO { public POINT Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int Size; public RECT Monitor, Work; public uint Flags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT { public int Length, Flags, ShowCmd; public POINT MinPosition, MaxPosition; public RECT NormalPosition; }

    [LibraryImport("user32.dll")] private static partial nint MonitorFromWindow(nint hwnd, uint flags);
    [LibraryImport("user32.dll")] private static partial nint MonitorFromRect(ref RECT rc, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetMonitorInfo(nint monitor, ref MONITORINFO info);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetCursorPos(out POINT p);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetWindowRect(nint hwnd, out RECT rc);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT wp);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool SetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT wp);
    [LibraryImport("user32.dll")] public static partial uint GetDpiForWindow(nint hwnd);
    [LibraryImport("dwmapi.dll")] private static partial int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    public static (RECT Monitor, RECT Work) MonitorOf(nint hwnd)
    {
        var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
        return (info.Monitor, info.Work);
    }

    public static bool IsOnAnyMonitor(RECT rc) => MonitorFromRect(ref rc, MONITOR_DEFAULTTONULL) != 0;

    public static void MoveResize(nint hwnd, int x, int y, int w, int h) => SetWindowPos(hwnd, 0, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

    public static double Scale(nint hwnd) => GetDpiForWindow(hwnd) / 96.0;

    /// <summary>Windows 11: rounded corners and the standard shadow on our custom-chrome window. Ignored on Windows 10.</summary>
    public static void UseRoundedCorners(nint hwnd)
    {
        var round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
    }

    /// <summary>Dark title-bar parts drawn by Windows (e.g. the system menu border on some builds).</summary>
    public static void UseDarkMode(nint hwnd, bool dark)
    {
        var v = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }

    /// <summary>A maximised custom-chrome window should fill the work area exactly, not spill under the taskbar.</summary>
    public static void FitMaximizedToWorkArea(nint hwnd, nint lParam)
    {
        var (monitor, work) = MonitorOf(hwnd);
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        mmi.MaxPosition.X = work.Left - monitor.Left;
        mmi.MaxPosition.Y = work.Top - monitor.Top;
        mmi.MaxSize.X = work.Width;
        mmi.MaxSize.Y = work.Height;
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    public static (int X, int Y) PointFromLParam(nint lParam) => ((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
}
