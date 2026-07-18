using System.Runtime.InteropServices;
using ScreenAnnotationTool.Interop;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class NativeMethodsTests
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    // A real native window is needed to verify these behaviors, and it must be VISIBLE:
    // Windows only applies HWND_TOPMOST (the WS_EX_TOPMOST bit) to visible windows —
    // matching the production overlay, which is shown before MakeInteractive runs.
    // Created 1x1 far offscreen so nothing ever appears on a developer's monitor.
    private static IntPtr CreateOffscreenTestWindow()
    {
        IntPtr hwnd = CreateWindowExW(
            0, "STATIC", "ChalkTest", WS_POPUP | WS_VISIBLE, -32000, -32000, 1, 1,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hwnd);
        return hwnd;
    }

    [Fact]
    public void MakeClickThrough_RejectsMissingWindowHandle()
    {
        Assert.Throws<ArgumentException>(() => NativeMethods.MakeClickThrough(IntPtr.Zero));
    }

    [Fact]
    public void MakeInteractive_RejectsMissingWindowHandle()
    {
        Assert.Throws<ArgumentException>(() => NativeMethods.MakeInteractive(IntPtr.Zero));
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [Fact]
    public void MoveToMonitorUnderCursor_RejectsMissingWindowHandle()
    {
        Assert.Throws<ArgumentException>(() => NativeMethods.MoveToMonitorUnderCursor(IntPtr.Zero));
    }

    // The overlay is positioned in raw physical pixels (SetWindowPos), never through
    // WPF's DIP conversion: that pipeline double-scaled Left on a 175% secondary
    // monitor, leaving most of the screen uncovered and clicks falling through.
    // The window must land exactly on the monitor's pixel rect.
    [Fact]
    public void MoveToMonitorUnderCursor_CoversTheFullMonitorExactly()
    {
        // Hidden window: the move still applies, and a monitor-sized STATIC window
        // must never flash on a developer's screen while tests run.
        IntPtr hwnd = CreateWindowExW(
            0, "STATIC", "ChalkTest", WS_POPUP, 0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hwnd);
        try
        {
            NativeMethods.MoveToMonitorUnderCursor(hwnd);
            Assert.True(GetWindowRect(hwnd, out NativeRect r));

            var (left, top, width, height) = NativeMethods.GetMonitorPhysicalRectFromCursor();
            Assert.Equal(left, r.Left);
            Assert.Equal(top, r.Top);
            Assert.Equal(width, r.Right - r.Left);
            Assert.Equal(height, r.Bottom - r.Top);
            Assert.True(width > 0 && height > 0);
        }
        finally
        {
            DestroyWindow(hwnd);
        }
    }

    [Fact]
    public void MakeClickThrough_SetsLayeredAndTransparentStyles()
    {
        IntPtr hwnd = CreateOffscreenTestWindow();
        try
        {
            NativeMethods.MakeClickThrough(hwnd);
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            Assert.NotEqual(0, ex & WS_EX_LAYERED);
            Assert.NotEqual(0, ex & WS_EX_TRANSPARENT);
        }
        finally
        {
            DestroyWindow(hwnd);
        }
    }

    [Fact]
    public void MakeInteractive_ClearsTransparencyAndReassertsTopmost()
    {
        IntPtr hwnd = CreateOffscreenTestWindow();
        try
        {
            NativeMethods.MakeClickThrough(hwnd);
            NativeMethods.MakeInteractive(hwnd);
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            Assert.NotEqual(0, ex & WS_EX_LAYERED);
            Assert.Equal(0, ex & WS_EX_TRANSPARENT);
            // The SetWindowPos nudge that flushes the cached style change must also
            // pin the window into the topmost band, observable as WS_EX_TOPMOST.
            Assert.NotEqual(0, ex & WS_EX_TOPMOST);
        }
        finally
        {
            DestroyWindow(hwnd);
        }
    }
}
