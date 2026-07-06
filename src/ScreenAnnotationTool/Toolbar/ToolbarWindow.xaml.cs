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
    private Button[] _toolButtons = Array.Empty<Button>();
    private static readonly Brush SelectedToolBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF));

    public event Action? UndoRequested;
    public event Action? ClearRequested;
    public event Action<bool>? StartupToggled;
    public event Action? CloseRequested;

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

        BtnRed.Click += (_, _) => _model.CurrentColor = Colors.Red;
        BtnYellow.Click += (_, _) => _model.CurrentColor = Colors.Yellow;
        BtnGreen.Click += (_, _) => _model.CurrentColor = Colors.LimeGreen;
        BtnBlue.Click += (_, _) => _model.CurrentColor = Colors.DodgerBlue;
        BtnBlack.Click += (_, _) => _model.CurrentColor = Colors.Black;

        BtnThin.Click += (_, _) => _model.CurrentThickness = 2;
        BtnMedium.Click += (_, _) => _model.CurrentThickness = 4;
        BtnThick.Click += (_, _) => _model.CurrentThickness = 8;

        BtnUndo.Click += (_, _) => UndoRequested?.Invoke();
        BtnClear.Click += (_, _) => ClearRequested?.Invoke();
        BtnClose.Click += (_, _) => CloseRequested?.Invoke();

        ChkStartup.IsChecked = _startup.IsEnabled();
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
        ChkStartup.IsChecked = _startup.IsEnabled();
        _suppressStartupEvent = false;
    }
}
