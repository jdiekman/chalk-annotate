using System;

namespace ScreenAnnotationTool.Services;

// System-tray icon backed by the in-box WinForms NotifyIcon (no external dependency).
// Types are fully-qualified because the WinForms/Drawing global usings are suppressed
// project-wide to avoid clashing with WPF's Point/Color/Application.
public class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly System.Drawing.Icon _icon;
    private readonly bool _ownsIcon;
    private bool _disposed;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _menu = new System.Windows.Forms.ContextMenuStrip();
        _menu.Items.Add("Show toolbar", null, (_, _) => ShowRequested?.Invoke());
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
        (_icon, _ownsIcon) = LoadIcon();

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Chalk",
            Visible = true,
            ContextMenuStrip = _menu
        };

        // Left-click (or double-click) the tray icon shows the toolbar.
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                ShowRequested?.Invoke();
        };
    }

    private static (System.Drawing.Icon Icon, bool OwnsIcon) LoadIcon()
    {
        // Prefer the embedded chalk.ico, picking the frame that matches the tray size.
        try
        {
            var res = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/chalk.ico"));
            if (res != null)
            {
                using var stream = res.Stream;
                using var source = new System.Drawing.Icon(
                    stream,
                    System.Windows.Forms.SystemInformation.SmallIconSize);
                return ((System.Drawing.Icon)source.Clone(), true);
            }
        }
        catch { /* fall back below */ }

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon != null)
                    return (icon, true);
            }
        }
        catch { /* fall back below */ }
        return (System.Drawing.SystemIcons.Application, false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        if (_ownsIcon)
            _icon.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
