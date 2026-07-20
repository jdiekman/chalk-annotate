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

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

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

        // SetWindowLong is documented to cache certain window data: the new ex-style may
        // not affect hit-testing until the window is nudged with SWP_FRAMECHANGED. Without
        // this, the overlay can stay click-through even though draw mode believes it is
        // interactive. HWND_TOPMOST simultaneously re-pins the overlay at the top of the
        // z-order in case a composited app (e.g. an animating browser) shuffled the band.
        if (!SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(), "Could not apply the overlay window style.");
        }
    }

    // Physical (device-pixel) rect of the monitor under the cursor; primary monitor
    // metrics on failure. Deliberately NOT converted to WPF DIPs: the DIP round-trip
    // (divide by monitor scale here, multiply back inside WPF) double-scaled the X
    // position on a 175% secondary monitor, leaving most of it uncovered.
    public static (int Left, int Top, int Width, int Height) GetMonitorPhysicalRectFromCursor()
    {
        if (GetCursorPos(out POINT pt))
        {
            IntPtr mon = MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(mon, ref mi))
            {
                var r = mi.rcMonitor;
                return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }
        }
        return (0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    }

    // Physical (device-pixel) rect of a window — used to map overlay-canvas points to
    // screen pixels for region capture without any DIP conversion.
    public static (int Left, int Top, int Width, int Height) GetWindowPhysicalRect(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("A valid window handle is required.", nameof(hwnd));
        if (!GetWindowRect(hwnd, out RECT r))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(), "Could not read the overlay window bounds.");
        }
        return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    // Position the overlay over the monitor under the cursor using raw device pixels,
    // bypassing WPF's Left/Top/Width/Height DIP conversion entirely (see above).
    public static void MoveToMonitorUnderCursor(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("A valid window handle is required.", nameof(hwnd));

        var (left, top, width, height) = GetMonitorPhysicalRectFromCursor();
        if (!SetWindowPos(hwnd, HWND_TOPMOST, left, top, width, height,
                SWP_NOACTIVATE | SWP_FRAMECHANGED))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(), "Could not position the overlay window.");
        }
    }
}
