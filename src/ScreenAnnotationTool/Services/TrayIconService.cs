using System;

namespace ScreenAnnotationTool.Services;

// System-tray icon backed by the in-box WinForms NotifyIcon (no external dependency).
// Types are fully-qualified because the WinForms/Drawing global usings are suppressed
// project-wide to avoid clashing with WPF's Point/Color/Application.
public class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show toolbar", null, (_, _) => ShowRequested?.Invoke());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Chalk",
            Visible = true,
            ContextMenuStrip = menu
        };

        // Left-click (or double-click) the tray icon shows the toolbar.
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                ShowRequested?.Invoke();
        };
    }

    private static System.Drawing.Icon LoadIcon()
    {
        // Prefer the embedded chalk.ico, picking the frame that matches the tray size.
        try
        {
            var res = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/chalk.ico"));
            if (res != null)
                return new System.Drawing.Icon(res.Stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        }
        catch { /* fall back below */ }

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon != null)
                    return icon;
            }
        }
        catch { /* fall back below */ }
        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
