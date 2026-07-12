using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScreenAnnotationTool.Drawing;
using ScreenAnnotationTool.Overlay;
using ScreenAnnotationTool.Services;
using ScreenAnnotationTool.Toolbar;

namespace ScreenAnnotationTool;

public class AppController
{
    private readonly AnnotationModel _model = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly StartupService _startup = StartupService.CreateDefault();
    private readonly SettingsService _settingsService;
    private readonly OverlayWindow _overlay;
    private readonly ToolbarWindow _toolbar;
    private readonly TrayIconService _tray = new();
    private AppSettings _settings = new();
    private bool _hasStarted;
    private bool _isShuttingDown;
    private bool _reallyExiting;

    public AppController()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = System.IO.Path.Combine(appData, "Chalk", "settings.json");
        _settingsService = new SettingsService(path);

        _overlay = new OverlayWindow(_model);
        _toolbar = new ToolbarWindow(_model, _startup);

        _hotkeys.HotkeyPressed += OnHotkey;
        _toolbar.DrawToggleRequested += ToggleDraw; // toolbar button mirrors Ctrl+Alt+D
        _toolbar.UndoRequested += () => _model.Undo();
        _toolbar.ClearRequested += ClearAll;
        _toolbar.StartupToggled += enabled =>
        {
            try
            {
                _startup.SetEnabled(enabled);
            }
            catch (Exception ex)
            {
                Dialogs.Show(
                    "Couldn't update the Windows startup setting:\n" + ex.Message,
                    MessageBoxImage.Warning);
                _toolbar.ReflectStartupState();
            }
        };
        // The X button hides the toolbar to the tray rather than quitting.
        _toolbar.HideRequested += HideToolbar;
        // Intercept Alt+F4 / any close: hide to tray instead, unless we're truly exiting.
        _toolbar.Closing += (_, e) =>
        {
            if (_reallyExiting) return;
            e.Cancel = true;
            HideToolbar();
        };

        // Tray icon: left-click / "Show toolbar" reveals it; "Exit" really quits.
        _tray.ShowRequested += ShowToolbar;
        _tray.ExitRequested += ExitApp;

        _overlay.PreviewKeyDown += OnOverlayKeyDown;
    }

    public void Start()
    {
        _settings = _settingsService.Load();
        SetToolbarPosition(_settings.ToolbarLeft, _settings.ToolbarTop);
        try { _model.CurrentColor = (Color)ColorConverter.ConvertFromString(_settings.ColorHex); }
        catch { _model.CurrentColor = Colors.Red; }
        _model.CurrentThickness = _settings.Thickness;

        // Show the overlay first (click-through) so the toolbar can be its owned window,
        // guaranteeing the toolbar always stays above the overlay and remains clickable.
        // The toolbar itself starts HIDDEN in the tray — Ctrl+Alt+D or the tray icon reveals it.
        _overlay.ShowOverlay();
        _toolbar.Owner = _overlay;
        _toolbar.SetDrawModeIndicator(false);
        _toolbar.UpdateColorSwatch();

        // A hotkey already owned by another app is an environmental fact, not a fatal
        // error: run without it and tell the user, instead of refusing to start.
        var unavailable = new List<string>();
        if (!_hotkeys.Register(HotkeyService.HotkeyToggleDraw, ModifierKeys.Control | ModifierKeys.Alt, Key.D))
            unavailable.Add("Ctrl+Alt+D (draw)");
        if (!_hotkeys.Register(HotkeyService.HotkeyClear, ModifierKeys.Control | ModifierKeys.Alt, Key.X))
            unavailable.Add("Ctrl+Alt+X (clear)");
        if (unavailable.Count > 0)
        {
            var list = string.Join(" and ", unavailable);
            Trace.TraceWarning("Hotkeys unavailable (in use by another app): {0}", list);
            Dialogs.Show(
                $"Another application is already using {list}.\n" +
                "Chalk will run without that hotkey; the toolbar buttons still work.",
                MessageBoxImage.Warning);
        }
        _hasStarted = true;
    }

    private void OnHotkey(int id)
    {
        switch (id)
        {
            case HotkeyService.HotkeyToggleDraw:
                ToggleDraw();
                break;
            case HotkeyService.HotkeyClear:
                ClearAll();
                break;
        }
    }

    private void ClearAll()
    {
        _overlay.CancelCurrentOperation();
        _model.Clear();
    }

    private void ToggleDraw()
    {
        if (_overlay.IsDrawing)
            _ = ExitDrawModeSafely();
        else
            EnterDrawModeSafely();
    }

    // Mirrors ExitDrawModeSafely: the one guarded doorway into draw mode, so any
    // future call site gets the same protection.
    private void EnterDrawModeSafely()
    {
        // Reveal the toolbar (if hidden in the tray) and start drawing immediately.
        if (!_toolbar.IsVisible)
            _toolbar.Show();
        try
        {
            _overlay.EnterDrawMode();
            _toolbar.SetDrawModeIndicator(true);
        }
        catch (Exception ex)
        {
            _toolbar.SetDrawModeIndicator(false);
            Dialogs.Show("Chalk couldn't make the overlay interactive:\n" + ex.Message);
        }
    }

    private void ShowToolbar()
    {
        if (!_toolbar.IsVisible)
            _toolbar.Show();
        _toolbar.Activate();
    }

    private void HideToolbar()
    {
        // Leaving the toolbar shouldn't strand an interactive, invisible overlay.
        if (_overlay.IsDrawing)
        {
            if (!ExitDrawModeSafely())
                return;
        }
        _toolbar.Hide();
    }

    private void ExitApp()
    {
        _reallyExiting = true;
        Shutdown();
        Application.Current.Shutdown();
    }

    private void OnOverlayKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _ = ExitDrawModeSafely();
            e.Handled = true;
        }
        else if (e.Key == Key.Z &&
                 (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
                return; // let the text box handle its own undo

            _model.Undo();
            e.Handled = true;
        }
    }

    public void Shutdown()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        // Settings persistence is best-effort; teardown below must always run first —
        // a modal dialog must never hold hotkeys, the tray icon, or the mutex hostage.
        string? saveError = null;
        try
        {
            if (_hasStarted)
            {
                _settings.ToolbarLeft = _toolbar.Left;
                _settings.ToolbarTop = _toolbar.Top;
                _settings.ColorHex = _model.CurrentColor.ToString();
                _settings.Thickness = _model.CurrentThickness;
                _settingsService.Save(_settings);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("Chalk couldn't save its settings: {0}", ex);
            saveError = ex.Message;
        }

        _reallyExiting = true;
        _hotkeys.Dispose();
        _tray.Dispose();
        _toolbar.Close();
        _overlay.Close();

        if (saveError != null && !Application.Current.Dispatcher.HasShutdownStarted)
            Dialogs.Show("Chalk couldn't save its settings:\n" + saveError, MessageBoxImage.Warning);
    }

    private bool ExitDrawModeSafely()
    {
        try
        {
            _overlay.ExitDrawMode();
            _toolbar.SetDrawModeIndicator(false);
            return true;
        }
        catch (Exception ex)
        {
            // Any failure while leaving a full-screen interactive overlay is unsafe.
            _overlay.Hide();
            Dialogs.Show("Chalk couldn't stop the overlay safely and will exit:\n" + ex.Message);
            ExitApp();
            return false;
        }
    }

    private void SetToolbarPosition(double left, double top)
    {
        double minLeft = SystemParameters.VirtualScreenLeft;
        double minTop = SystemParameters.VirtualScreenTop;
        double maxLeft = minLeft + Math.Max(0, SystemParameters.VirtualScreenWidth - 100);
        double maxTop = minTop + Math.Max(0, SystemParameters.VirtualScreenHeight - 40);

        _toolbar.Left = Math.Clamp(left, minLeft, maxLeft);
        _toolbar.Top = Math.Clamp(top, minTop, maxTop);
    }
}
