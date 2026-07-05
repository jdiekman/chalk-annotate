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
    private AppSettings _settings = new();

    public AppController()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = System.IO.Path.Combine(appData, "ScreenAnnotationTool", "settings.json");
        _settingsService = new SettingsService(path);

        _overlay = new OverlayWindow(_model);
        _toolbar = new ToolbarWindow(_model, _startup);

        _hotkeys.HotkeyPressed += OnHotkey;
        _toolbar.UndoRequested += () => _model.Undo();
        _toolbar.ClearRequested += () => _model.Clear();
        _toolbar.StartupToggled += enabled => _startup.SetEnabled(enabled);
        _toolbar.CloseRequested += () =>
        {
            Shutdown();
            Application.Current.Shutdown();
        };

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

        _toolbar.Show();
        _toolbar.SetDrawModeIndicator(false);

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
            _overlay.EnterDrawMode();
            _toolbar.SetDrawModeIndicator(true);
        }
    }

    private void OnOverlayKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _overlay.ExitDrawMode();
            _toolbar.SetDrawModeIndicator(false);
        }
        else if (e.Key == Key.Z &&
                 (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _model.Undo();
        }
    }

    public void Shutdown()
    {
        _settings.ToolbarLeft = _toolbar.Left;
        _settings.ToolbarTop = _toolbar.Top;
        _settings.ColorHex = _model.CurrentColor.ToString();
        _settings.Thickness = _model.CurrentThickness;
        _settings.LaunchOnStartup = _startup.IsEnabled();
        _settingsService.Save(_settings);

        _hotkeys.Dispose();
        _overlay.Close();
    }
}
