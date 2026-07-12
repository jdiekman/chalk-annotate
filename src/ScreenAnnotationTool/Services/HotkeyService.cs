using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenAnnotationTool.Services;

public class HotkeyService : IDisposable
{
    public const int HotkeyToggleDraw = 1;
    public const int HotkeyClear = 2;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly HashSet<int> _ids = new();
    private bool _disposed;

    public event Action<int>? HotkeyPressed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("SAT_HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE — makes this a message-only window
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    // Returns false when the combination is already owned by another application —
    // an environmental condition the caller should degrade on, not a fatal error.
    public bool Register(int id, ModifierKeys modifiers, Key key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ids.Contains(id))
            throw new InvalidOperationException($"Hotkey id {id} is already registered.");

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        uint mods = MOD_NOREPEAT;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= 0x0008;

        if (!RegisterHotKey(_source.Handle, id, mods, vk))
            return false;

        _ids.Add(id);
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = unchecked((int)wParam.ToInt64());
            if (_ids.Contains(id))
            {
                HotkeyPressed?.Invoke(id);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var id in _ids)
            UnregisterHotKey(_source.Handle, id);
        _ids.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
