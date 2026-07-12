using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenAnnotationTool.Interop;

public static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;

    // Only GWL_EXSTYLE (32-bit style flags) is read/written here, so the 32-bit
    // GetWindowLong/SetWindowLong are safe on x64; no GetWindowLongPtr needed.
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

    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public static void MakeClickThrough(IntPtr hwnd)
    {
        UpdateExtendedStyle(hwnd, ex => ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    public static void MakeInteractive(IntPtr hwnd)
    {
        UpdateExtendedStyle(hwnd, ex => (ex | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT);
    }

    private static void UpdateExtendedStyle(IntPtr hwnd, Func<int, int> update)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("A valid window handle is required.", nameof(hwnd));

        Marshal.SetLastPInvokeError(0);
        int current = GetWindowLong(hwnd, GWL_EXSTYLE);
        int error = Marshal.GetLastPInvokeError();
        if (current == 0 && error != 0)
            throw new Win32Exception(error, "Could not read the overlay window style.");

        Marshal.SetLastPInvokeError(0);
        int previous = SetWindowLong(hwnd, GWL_EXSTYLE, update(current));
        error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0)
            throw new Win32Exception(error, "Could not update the overlay window style.");
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

        double scaleX = 1.0;
        double scaleY = 1.0;
        int hr = GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
        if (hr == 0 && dpiX != 0 && dpiY != 0)
        {
            scaleX = dpiX / 96.0;
            scaleY = dpiY / 96.0;
        }

        return new Rect(
            r.Left / scaleX,
            r.Top / scaleY,
            (r.Right - r.Left) / scaleX,
            (r.Bottom - r.Top) / scaleY);
    }
}
