using System;
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
        _toolbar.ClearRequested += () => _model.Clear();
        _toolbar.StartupToggled += enabled =>
        {
            try
            {
                _startup.SetEnabled(enabled);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Couldn't update the Windows startup setting:\n" + ex.Message,
                    "Chalk", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
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
        _toolbar.Left = _settings.ToolbarLeft;
        _toolbar.Top = _settings.ToolbarTop;
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

        _hotkeys.Register(HotkeyService.HotkeyToggleDraw, ModifierKeys.Control | ModifierKeys.Alt, Key.D);
        _hotkeys.Register(HotkeyService.HotkeyClear, ModifierKeys.Control | ModifierKeys.Alt, Key.X);
    }

    private void OnHotkey(int id)
    {
        switch (id)
        {
            case HotkeyService.HotkeyToggleDraw:
                ToggleDraw();
                break;
            case HotkeyService.HotkeyClear:
                _model.Clear();
                break;
        }
    }

    private void ToggleDraw()
    {
        if (_overlay.IsDrawing)
        {
            _overlay.ExitDrawMode();
            _toolbar.SetDrawModeIndicator(false);
        }
        else
        {
            // Reveal the toolbar (if hidden in the tray) and start drawing immediately.
            if (!_toolbar.IsVisible)
                _toolbar.Show();
            _overlay.EnterDrawMode();
            _toolbar.SetDrawModeIndicator(true);
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
            _overlay.ExitDrawMode();
            _toolbar.SetDrawModeIndicator(false);
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
            _overlay.ExitDrawMode();
            _toolbar.SetDrawModeIndicator(false);
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

        try
        {
            _settings.ToolbarLeft = _toolbar.Left;
            _settings.ToolbarTop = _toolbar.Top;
            _settings.ColorHex = _model.CurrentColor.ToString();
            _settings.Thickness = _model.CurrentThickness;
            _settings.LaunchOnStartup = _startup.IsEnabled();
            _settingsService.Save(_settings);
        }
        catch
        {
            // Settings persistence is best-effort; teardown below must still happen.
        }

        _hotkeys.Dispose();
        _tray.Dispose();
        _overlay.Close();
    }
}
