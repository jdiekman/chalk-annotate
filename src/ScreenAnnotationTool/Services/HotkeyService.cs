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

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly List<int> _ids = new();

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

    public void Register(int id, ModifierKeys modifiers, Key key)
    {
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        uint mods = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= 0x0008;

        if (RegisterHotKey(_source.Handle, id, mods, vk))
            _ids.Add(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke((int)wParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _ids)
            UnregisterHotKey(_source.Handle, id);
        _ids.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
