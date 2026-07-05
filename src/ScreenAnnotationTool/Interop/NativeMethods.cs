using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenAnnotationTool.Interop;

public static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static void MakeClickThrough(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    public static void MakeInteractive(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_LAYERED;
        ex &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    public static Rect GetMonitorBoundsFromCursor()
    {
        if (!GetCursorPos(out POINT pt))
            return SystemParameters.WorkArea;

        IntPtr mon = MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi))
            return SystemParameters.WorkArea;

        var r = mi.rcMonitor;
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
