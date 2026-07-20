using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenAnnotationTool.Drawing;
using ScreenAnnotationTool.Interop;

namespace ScreenAnnotationTool.Overlay;

public partial class OverlayWindow : Window
{
    private const double MinimumFreehandPointDistance = 1.5;
    private const int MaximumFreehandPoints = 8192;

    private readonly AnnotationModel _model;
    private IntPtr _hwnd;

    private bool _dragging;
    private Point _start;
    private Shape? _preview;      // rubber-band shape being sized
    private Polyline? _freehand;  // active freehand polyline
    private TextBox? _activeText; // active text entry box

    private bool _selectingCapture;            // capture-selection mode active
    private Rectangle? _captureBandBack;       // dark backing stroke of the marquee
    private Rectangle? _captureBandFront;      // white dashed stroke on top

    public bool IsDrawing { get; private set; }

    // Raised with the selected region in physical screen pixels.
    public event Action<Int32Rect>? CaptureRegionSelected;

    public OverlayWindow(AnnotationModel model)
    {
        InitializeComponent();
        _model = model;
        _model.Changed += OnModelChanged;

        SourceInitialized += OnSourceInitialized;
        DrawCanvas.MouseLeftButtonDown += OnMouseDown;
        DrawCanvas.MouseMove += OnMouseMove;
        DrawCanvas.MouseLeftButtonUp += OnMouseUp;
        DrawCanvas.LostMouseCapture += OnLostMouseCapture;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        try
        {
            NativeMethods.MakeClickThrough(_hwnd); // off by default
        }
        catch (Exception ex)
        {
            // Degrade instead of aborting launch: hide the overlay so it can't intercept
            // input; Enter/ExitDrawMode re-attempt the style change under their own guards.
            System.Diagnostics.Trace.TraceWarning(
                "Chalk couldn't make the overlay click-through at startup: {0}", ex);
            Hide();
        }
    }

    // Show the overlay at startup in click-through mode (transparent, harmless) so it exists
    // and can act as the owner of the toolbar window (keeping the toolbar always above it).
    public void ShowOverlay()
    {
        Show(); // SourceInitialized applies click-through by default and sets _hwnd
        try
        {
            NativeMethods.MoveToMonitorUnderCursor(_hwnd);
        }
        catch (Exception ex)
        {
            // Startup placement is cosmetic while click-through; EnterDrawMode retries.
            System.Diagnostics.Trace.TraceWarning(
                "Chalk couldn't position the overlay at startup: {0}", ex);
        }
    }

    public void EnterDrawMode()
    {
        Show();
        // Positioned via SetWindowPos in physical pixels, NOT via Left/Top/Width/Height:
        // WPF's DIP conversion double-scaled the X position on a scaled secondary
        // monitor, leaving most of it uncovered so clicks fell through to the app below.
        NativeMethods.MoveToMonitorUnderCursor(_hwnd);
        NativeMethods.MakeInteractive(_hwnd);
        IsDrawing = true;
        Activate();
    }

    public void ExitDrawMode()
    {
        try
        {
            // Cancel transient work BEFORE Clear (same order as ClearAll) so the Cleared
            // handler doesn't re-add a doomed preview/stroke only to remove it again.
            // Committing text first would be pointless: Clear wipes it immediately.
            CancelCurrentOperation();
            _model.Clear(); // wipe annotations when leaving draw mode
        }
        finally
        {
            CancelActiveStroke();
            IsDrawing = false;
            if (_hwnd != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.MakeClickThrough(_hwnd);
                }
                catch
                {
                    // Never leave a topmost, full-screen window intercepting input.
                    Hide();
                    throw;
                }
            }
        }
    }

    private void OnModelChanged(object? sender, AnnotationChangedEventArgs e)
    {
        switch (e.Kind)
        {
            case AnnotationChangeKind.Added when e.Annotation != null:
                DrawCanvas.Children.Add(e.Annotation);
                break;
            case AnnotationChangeKind.Removed when e.Annotation != null:
                DrawCanvas.Children.Remove(e.Annotation);
                break;
            case AnnotationChangeKind.Cleared:
                DrawCanvas.Children.Clear();
                AddTransientElements();
                break;
        }
    }

    // Arm capture-selection: the next drag draws a marquee instead of an annotation.
    public void BeginCaptureRegionSelection()
    {
        if (!IsDrawing || _selectingCapture) return;
        CommitActiveText();
        CancelActiveStroke();
        _selectingCapture = true;
        DrawCanvas.Cursor = Cursors.Cross;
    }

    // Returns true when a selection was actually in progress (lets Escape cancel the
    // selection without also leaving draw mode).
    public bool CancelCaptureRegionSelection()
    {
        if (!_selectingCapture) return false;
        _selectingCapture = false;
        _dragging = false;
        if (DrawCanvas.IsMouseCaptured)
            DrawCanvas.ReleaseMouseCapture();
        RemoveCaptureBand();
        DrawCanvas.Cursor = null;
        return true;
    }

    private void RemoveCaptureBand()
    {
        if (_captureBandBack != null)
            DrawCanvas.Children.Remove(_captureBandBack);
        if (_captureBandFront != null)
            DrawCanvas.Children.Remove(_captureBandFront);
        _captureBandBack = null;
        _captureBandFront = null;
    }

    private void UpdateCaptureBand(Point p)
    {
        if (_captureBandBack == null || _captureBandFront == null) return;
        double left = Math.Min(_start.X, p.X);
        double top = Math.Min(_start.Y, p.Y);
        double width = Math.Abs(p.X - _start.X);
        double height = Math.Abs(p.Y - _start.Y);
        foreach (var band in new[] { _captureBandBack, _captureBandFront })
        {
            Canvas.SetLeft(band, left);
            Canvas.SetTop(band, top);
            band.Width = width;
            band.Height = height;
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsDrawing) return;
        if (_selectingCapture)
        {
            _start = e.GetPosition(DrawCanvas);
            _captureBandBack = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromArgb(0xB0, 0x00, 0x00, 0x00)),
                StrokeThickness = 1.0,
                Fill = Brushes.Transparent
            };
            _captureBandFront = new Rectangle
            {
                Stroke = Brushes.White,
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = Brushes.Transparent
            };
            DrawCanvas.Children.Add(_captureBandBack);
            DrawCanvas.Children.Add(_captureBandFront);
            UpdateCaptureBand(_start);
            _dragging = true;
            if (!DrawCanvas.CaptureMouse())
                CancelCaptureRegionSelection();
            return;
        }
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
        if (!DrawCanvas.CaptureMouse())
            CancelActiveStroke();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDrawing || !_dragging) return;
        var p = e.GetPosition(DrawCanvas);

        if (_selectingCapture)
        {
            UpdateCaptureBand(p);
            return;
        }

        if (_model.CurrentTool == ToolKind.Freehand)
        {
            AddFreehandPoint(p, force: false);
            return;
        }

        if (_preview != null)
            DrawCanvas.Children.Remove(_preview);

        _preview = _model.CurrentTool switch
        {
            ToolKind.Rectangle => ShapeFactory.CreateRectangle(_start, p, _model.CurrentColor, _model.CurrentThickness),
            ToolKind.Ellipse => ShapeFactory.CreateEllipse(_start, p, _model.CurrentColor, _model.CurrentThickness),
            ToolKind.Arrow => ShapeFactory.CreateArrow(_start, p, _model.CurrentColor, _model.CurrentThickness),
            _ => null
        };
        if (_preview != null)
            DrawCanvas.Children.Add(_preview);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsDrawing || !_dragging) return;
        if (_selectingCapture)
        {
            var end = e.GetPosition(DrawCanvas);
            // The marquee must be gone (and the canvas re-rendered) before the caller
            // blits the screen, or it would appear in the capture itself.
            CancelCaptureRegionSelection();
            var region = MapCanvasDragToPhysicalRegion(_start, end);
            if (region.HasValue)
                CaptureRegionSelected?.Invoke(region.Value);
            return;
        }
        _dragging = false;
        DrawCanvas.ReleaseMouseCapture();

        if (_model.CurrentTool == ToolKind.Freehand)
        {
            if (_freehand != null)
            {
                AddFreehandPoint(e.GetPosition(DrawCanvas), force: true);
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
        var box = new TextBox
        {
            MinWidth = 40,
            Foreground = new System.Windows.Media.SolidColorBrush(_model.CurrentColor),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = new System.Windows.Media.SolidColorBrush(_model.CurrentColor),
            FontSize = 12 + _model.CurrentThickness * 3
        };
        _activeText = box;
        Canvas.SetLeft(box, at.X);
        Canvas.SetTop(box, at.Y);
        DrawCanvas.Children.Add(box);
        box.Focus();
        // Both handlers must check they still belong to the CURRENT box: WPF keeps keyboard
        // focus on a text box even after it is detached from the tree, so a previous box's
        // LostFocus can fire later — while a newer box occupies _activeText. Unguarded, that
        // stale event commits/nulls the new box mid-StartText (NullReference crash).
        box.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter && ReferenceEquals(box, _activeText)) CommitActiveText();
        };
        box.LostFocus += (_, _) =>
        {
            if (ReferenceEquals(box, _activeText)) CommitActiveText();
        };
    }

    // Map a drag on the canvas (DIPs) to physical screen pixels proportionally against
    // the window's physical rect — deliberately avoiding DPI factors, matching how the
    // overlay itself is positioned.
    private Int32Rect? MapCanvasDragToPhysicalRegion(Point a, Point b)
    {
        if (_hwnd == IntPtr.Zero) return null;
        double canvasWidth = DrawCanvas.ActualWidth;
        double canvasHeight = DrawCanvas.ActualHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0) return null;

        var (winLeft, winTop, winWidth, winHeight) = NativeMethods.GetWindowPhysicalRect(_hwnd);
        double scaleX = winWidth / canvasWidth;
        double scaleY = winHeight / canvasHeight;

        return Services.CaptureService.NormalizeRegion(
            winLeft + (int)Math.Round(a.X * scaleX),
            winTop + (int)Math.Round(a.Y * scaleY),
            winLeft + (int)Math.Round(b.X * scaleX),
            winTop + (int)Math.Round(b.Y * scaleY),
            new Int32Rect(winLeft, winTop, winWidth, winHeight));
    }

    // Discard the in-progress text box without committing it — used by "clear all",
    // where committing would resurrect the text the user is trying to wipe.
    public void CancelCurrentOperation()
    {
        CancelCaptureRegionSelection();
        DetachActiveText();
        CancelActiveStroke();
    }

    private void CommitActiveText()
    {
        var box = DetachActiveText();
        if (box == null) return;

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

    private TextBox? DetachActiveText()
    {
        if (_activeText == null) return null;
        var box = _activeText;
        _activeText = null;
        DrawCanvas.Children.Remove(box);
        // Never leave keyboard focus on the detached element — that is what produces the
        // delayed stale LostFocus in the first place.
        if (box.IsKeyboardFocused)
            Keyboard.ClearFocus();
        return box;
    }

    private void CancelActiveStroke()
    {
        _dragging = false;
        if (DrawCanvas.IsMouseCaptured)
            DrawCanvas.ReleaseMouseCapture();

        if (_preview != null)
            DrawCanvas.Children.Remove(_preview);
        if (_freehand != null)
            DrawCanvas.Children.Remove(_freehand);
        _preview = null;
        _freehand = null;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        if (_selectingCapture)
            CancelCaptureRegionSelection();
        else
            CancelActiveStroke();
    }

    private void AddFreehandPoint(Point point, bool force)
    {
        if (_freehand == null)
            return;

        Point last = _freehand.Points[^1];
        Vector delta = point - last;
        double minimumSquared = MinimumFreehandPointDistance * MinimumFreehandPointDistance;
        if (!force && delta.LengthSquared < minimumSquared)
            return;
        if (force && point == last)
            return;

        _freehand.Points.Add(point);
        if (_freehand.Points.Count > MaximumFreehandPoints)
            SimplifyFreehandPoints(_freehand);
    }

    private static void SimplifyFreehandPoints(Polyline freehand)
    {
        var source = freehand.Points;
        var simplified = new PointCollection((source.Count / 2) + 1);
        for (int i = 0; i < source.Count; i += 2)
            simplified.Add(source[i]);
        if (simplified[^1] != source[^1])
            simplified.Add(source[^1]);
        freehand.Points = simplified;
    }

    private void AddTransientElements()
    {
        if (_preview != null)
            DrawCanvas.Children.Add(_preview);
        if (_freehand != null)
            DrawCanvas.Children.Add(_freehand);
        if (_activeText != null)
            DrawCanvas.Children.Add(_activeText);
    }
}
