using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScreenAnnotationTool.Drawing;
using ScreenAnnotationTool.Services;

namespace ScreenAnnotationTool.Toolbar;

public partial class ToolbarWindow : Window
{
    private readonly AnnotationModel _model;
    private readonly StartupService _startup;

    public event Action? UndoRequested;
    public event Action? ClearRequested;
    public event Action<bool>? StartupToggled;
    public event Action? CloseRequested;

    public ToolbarWindow(AnnotationModel model, StartupService startup)
    {
        InitializeComponent();
        _model = model;
        _startup = startup;

        Root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        BtnFreehand.Click += (_, _) => _model.CurrentTool = ToolKind.Freehand;
        BtnArrow.Click += (_, _) => _model.CurrentTool = ToolKind.Arrow;
        BtnRect.Click += (_, _) => _model.CurrentTool = ToolKind.Rectangle;
        BtnEllipse.Click += (_, _) => _model.CurrentTool = ToolKind.Ellipse;
        BtnText.Click += (_, _) => _model.CurrentTool = ToolKind.Text;

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
        ChkStartup.Checked += (_, _) => StartupToggled?.Invoke(true);
        ChkStartup.Unchecked += (_, _) => StartupToggled?.Invoke(false);
    }

    public void SetDrawModeIndicator(bool on)
    {
        DrawIndicator.Fill = on ? Brushes.LimeGreen : Brushes.Gray;
    }
}
