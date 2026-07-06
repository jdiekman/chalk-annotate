using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using ScreenAnnotationTool.Drawing;
using ScreenAnnotationTool.Interop;

namespace ScreenAnnotationTool.Overlay;

public partial class OverlayWindow : Window
{
    private readonly AnnotationModel _model;
    private IntPtr _hwnd;

    private bool _dragging;
    private Point _start;
    private Shape? _preview;      // rubber-band shape being sized
    private Polyline? _freehand;  // active freehand polyline
    private TextBox? _activeText; // active text entry box

    public bool IsDrawing { get; private set; }

    public OverlayWindow(AnnotationModel model)
    {
        InitializeComponent();
        _model = model;
        _model.Changed += (_, _) => SyncCanvas();

        SourceInitialized += OnSourceInitialized;
        DrawCanvas.MouseLeftButtonDown += OnMouseDown;
        DrawCanvas.MouseMove += OnMouseMove;
        DrawCanvas.MouseLeftButtonUp += OnMouseUp;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeClickThrough(_hwnd); // off by default
    }

    public void EnterDrawMode()
    {
        var bounds = NativeMethods.GetMonitorBoundsFromCursor();
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        Show();
        IsDrawing = true;
        NativeMethods.MakeInteractive(_hwnd);
        Activate();
    }

    public void ExitDrawMode()
    {
        CommitActiveText();

        if (_dragging)
        {
            DrawCanvas.ReleaseMouseCapture();
            _dragging = false;
        }
        if (_preview != null)
        {
            DrawCanvas.Children.Remove(_preview);
            _preview = null;
        }
        if (_freehand != null)
        {
            DrawCanvas.Children.Remove(_freehand);
            _freehand = null;
        }

        IsDrawing = false;
        if (_hwnd != IntPtr.Zero)
            NativeMethods.MakeClickThrough(_hwnd);
    }

    private void SyncCanvas()
    {
        // Rebuild committed annotations; keep any in-progress preview/text.
        DrawCanvas.Children.Clear();
        foreach (var a in _model.Annotations)
            if (a is UIElement el)
                DrawCanvas.Children.Add(el);
        if (_preview != null) DrawCanvas.Children.Add(_preview);
        if (_freehand != null) DrawCanvas.Children.Add(_freehand);
        if (_activeText != null) DrawCanvas.Children.Add(_activeText);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsDrawing) return;
        CommitActiveText();
        _start = e.GetPosition(DrawCanvas);

        if (_model.CurrentTool == ToolKind.Text)
        {
            StartText(_start);
            return;
        }
        if (_model.CurrentTool == ToolKind.Freehand)
        {
            _freehand = ShapeFactory.StartFreehand(_start, _model.CurrentColor, _model.CurrentThickness);
            DrawCanvas.Children.Add(_freehand);
        }
        _dragging = true;
        DrawCanvas.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDrawing || !_dragging) return;
        var p = e.GetPosition(DrawCanvas);

        if (_model.CurrentTool == ToolKind.Freehand)
        {
            _freehand?.Points.Add(p);
            return;
        }

        if (_preview != null)
            DrawCanvas.Children.Remove(_preview);

        _preview = _model.CurrentTool switch
        {
            ToolKind.Rectangle => ShapeFactory.CreateRectangle(_start, p, _model.CurrentColor, _model.CurrentThickness),
            ToolKind.Ellipse   => ShapeFactory.CreateEllipse(_start, p, _model.CurrentColor, _model.CurrentThickness),
            ToolKind.Arrow     => ShapeFactory.CreateArrow(_start, p, _model.CurrentColor, _model.CurrentThickness),
            _ => null
        };
        if (_preview != null)
            DrawCanvas.Children.Add(_preview);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsDrawing || !_dragging) return;
        _dragging = false;
        DrawCanvas.ReleaseMouseCapture();

        if (_model.CurrentTool == ToolKind.Freehand)
        {
            if (_freehand != null)
            {
                var f = _freehand;
                _freehand = null;
                DrawCanvas.Children.Remove(f);
                if (f.Points.Count > 1)
                    _model.Add(f);
            }
            return;
        }

        if (_preview != null)
        {
            var shape = _preview;
            _preview = null;
            DrawCanvas.Children.Remove(shape);
            _model.Add(shape);
        }
    }

    private void StartText(Point at)
    {
        _activeText = new TextBox
        {
            MinWidth = 40,
            Foreground = new System.Windows.Media.SolidColorBrush(_model.CurrentColor),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = new System.Windows.Media.SolidColorBrush(_model.CurrentColor),
            FontSize = 12 + _model.CurrentThickness * 3
        };
        Canvas.SetLeft(_activeText, at.X);
        Canvas.SetTop(_activeText, at.Y);
        DrawCanvas.Children.Add(_activeText);
        _activeText.Focus();
        _activeText.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter) CommitActiveText();
        };
        _activeText.LostFocus += (_, _) => CommitActiveText();
    }

    private void CommitActiveText()
    {
        if (_activeText == null) return;
        var box = _activeText;
        _activeText = null;
        DrawCanvas.Children.Remove(box);

        if (!string.IsNullOrWhiteSpace(box.Text))
        {
            var tb = new TextBlock
            {
                Text = box.Text,
                Foreground = box.Foreground,
                FontSize = box.FontSize
            };
            Canvas.SetLeft(tb, Canvas.GetLeft(box));
            Canvas.SetTop(tb, Canvas.GetTop(box));
            _model.Add(tb);
        }
    }
}
