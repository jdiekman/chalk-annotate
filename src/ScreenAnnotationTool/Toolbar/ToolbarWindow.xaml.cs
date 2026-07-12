using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenAnnotationTool.Drawing;
using ScreenAnnotationTool.Services;

namespace ScreenAnnotationTool.Toolbar;

public partial class ToolbarWindow : Window
{
    private readonly AnnotationModel _model;
    private readonly StartupService _startup;
    private bool _suppressStartupEvent = false;
    private readonly Button[] _toolButtons;
    private static readonly Brush SelectedToolBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF));

    public event Action? UndoRequested;
    public event Action? ClearRequested;
    public event Action<bool>? StartupToggled;
    public event Action? HideRequested;
    public event Action? DrawToggleRequested;

    public ToolbarWindow(AnnotationModel model, StartupService startup)
    {
        InitializeComponent();
        _model = model;
        _startup = startup;

        // Drag the whole toolbar by its background. Clicks on the Buttons/CheckBox
        // inside Root don't reach here because ButtonBase marks MouseLeftButtonDown
        // as handled. DragMove() is wrapped because it throws InvalidOperationException
        // if the left button is released in the brief race between event and the move.
        Root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); }
            catch (InvalidOperationException) { /* button released mid-drag; ignore */ }
        };

        _toolButtons = new[] { BtnFreehand, BtnArrow, BtnRect, BtnEllipse, BtnText };
        BtnFreehand.Click += (_, _) => SelectTool(ToolKind.Freehand, BtnFreehand);
        BtnArrow.Click += (_, _) => SelectTool(ToolKind.Arrow, BtnArrow);
        BtnRect.Click += (_, _) => SelectTool(ToolKind.Rectangle, BtnRect);
        BtnEllipse.Click += (_, _) => SelectTool(ToolKind.Ellipse, BtnEllipse);
        BtnText.Click += (_, _) => SelectTool(ToolKind.Text, BtnText);
        HighlightTool(BtnFreehand); // default tool

        BuildColorGrid();
        BtnColor.Click += (_, _) => ColorPopup.IsOpen = !ColorPopup.IsOpen;
        UpdateColorSwatch();

        BtnThin.Click += (_, _) => _model.CurrentThickness = 2;
        BtnMedium.Click += (_, _) => _model.CurrentThickness = 4;
        BtnThick.Click += (_, _) => _model.CurrentThickness = 8;

        BtnToggleDraw.Click += (_, _) => DrawToggleRequested?.Invoke();
        BtnUndo.Click += (_, _) => UndoRequested?.Invoke();
        BtnClear.Click += (_, _) => ClearRequested?.Invoke();
        BtnClose.Click += (_, _) => HideRequested?.Invoke();

        try { ChkStartup.IsChecked = _startup.IsEnabled(); }
        catch { ChkStartup.IsChecked = false; }
        ChkStartup.Checked += (_, _) =>
        {
            if (_suppressStartupEvent) return;
            StartupToggled?.Invoke(true);
        };
        ChkStartup.Unchecked += (_, _) =>
        {
            if (_suppressStartupEvent) return;
            StartupToggled?.Invoke(false);
        };
    }

    public void SetDrawModeIndicator(bool on)
    {
        DrawIndicator.Fill = on ? Brushes.LimeGreen : Brushes.Gray;
    }

    // Five colour families, five light→dark shades each (25 swatches).
    private static readonly string[][] ColorShades =
    {
        new[] { "#EF9A9A", "#EF5350", "#F44336", "#D32F2F", "#B71C1C" }, // Red
        new[] { "#FFF59D", "#FFEE58", "#FDD835", "#F9A825", "#F57F17" }, // Yellow
        new[] { "#A5D6A7", "#66BB6A", "#4CAF50", "#388E3C", "#1B5E20" }, // Green
        new[] { "#90CAF9", "#42A5F5", "#2196F3", "#1976D2", "#0D47A1" }, // Blue
        new[] { "#BDBDBD", "#9E9E9E", "#616161", "#424242", "#000000" }, // Gray → Black
    };

    private void BuildColorGrid()
    {
        foreach (var family in ColorShades)
        {
            foreach (var hex in family)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var swatch = new Button
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    BorderThickness = new Thickness(1),
                    ToolTip = hex
                };
                swatch.Click += (_, _) => SelectColor(color);
                SwatchGrid.Children.Add(swatch);
            }
        }
    }

    private void SelectColor(Color color)
    {
        _model.CurrentColor = color;
        UpdateColorSwatch();
        ColorPopup.IsOpen = false;
    }

    // Reflect the model's current colour on the toolbar's colour swatch.
    public void UpdateColorSwatch()
    {
        BtnColor.Background = new SolidColorBrush(_model.CurrentColor);
    }

    private void SelectTool(ToolKind tool, Button button)
    {
        _model.CurrentTool = tool;
        HighlightTool(button);
    }

    private void HighlightTool(Button active)
    {
        foreach (var b in _toolButtons)
        {
            b.BorderThickness = new Thickness(0);
            b.BorderBrush = null;
        }
        active.BorderBrush = SelectedToolBrush;
        active.BorderThickness = new Thickness(2);
    }

    public void ReflectStartupState()
    {
        _suppressStartupEvent = true;
        try
        {
            ChkStartup.IsChecked = _startup.IsEnabled();
        }
        catch
        {
            ChkStartup.IsChecked = false;
        }
        finally
        {
            _suppressStartupEvent = false;
        }
    }
}
