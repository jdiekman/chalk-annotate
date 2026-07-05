# Screen Annotation Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows hotkey-triggered transparent screen-annotation overlay (draw rectangles, circles, arrows, freehand, text) for pointing things out during tutorial recordings.

**Architecture:** A WPF (.NET 8) app with three parts — a full-monitor transparent `OverlayWindow` that toggles between click-through and draw-capturing via Win32 extended window styles, a floating `ToolbarWindow` for tool/color/thickness selection, and a `HotkeyService` registering system-wide hotkeys. A UI-free `AnnotationModel` owns tool/color/thickness state and the annotation list (unit-tested); an `AppController` is the single source of truth wiring hotkeys + toolbar to the model + overlay.

**Tech Stack:** C# / .NET 8, WPF, Win32 P/Invoke (`user32.dll`, `advapi32.dll` via registry APIs), xUnit for tests.

## Global Constraints

- Target framework: `net8.0-windows`, `<UseWPF>true</UseWPF>`.
- Platform: Windows 11 only. Win32 interop is expected and acceptable.
- Overlay window chrome: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`.
- Overlay appears on the monitor containing the cursor at draw-mode toggle-on; fall back to primary monitor if unresolved.
- Default hotkeys (fixed in v1, all defined in one constants location): Ctrl+Alt+D toggle draw mode, Ctrl+Alt+X clear all, Ctrl+Z undo (draw mode active), Esc exit draw mode.
- Settings persisted as JSON at `%APPDATA%\ScreenAnnotationTool\settings.json`.
- Startup registry key: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `ScreenAnnotationTool`.
- Out of scope for v1: multi-monitor spanning, spotlight/highlighter, screen zoom, saving annotations to file, rebindable-hotkey UI.

---

### Task 1: Solution scaffold + project structure

**Files:**
- Create: `ScreenAnnotationTool.sln`
- Create: `src/ScreenAnnotationTool/ScreenAnnotationTool.csproj`
- Create: `tests/ScreenAnnotationTool.Tests/ScreenAnnotationTool.Tests.csproj`
- Create: `src/ScreenAnnotationTool/App.xaml`, `src/ScreenAnnotationTool/App.xaml.cs`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: a buildable WPF app project `ScreenAnnotationTool` and an xUnit test project referencing it.

- [ ] **Step 1: Create the WPF app project**

Run from repo root (`c:/Dev/Annotation-Tool`):
```bash
dotnet new wpf -n ScreenAnnotationTool -o src/ScreenAnnotationTool -f net8.0
```

- [ ] **Step 2: Create the xUnit test project**

```bash
dotnet new xunit -n ScreenAnnotationTool.Tests -o tests/ScreenAnnotationTool.Tests -f net8.0
```

- [ ] **Step 3: Create the solution and add both projects**

```bash
dotnet new sln -n ScreenAnnotationTool
dotnet sln add src/ScreenAnnotationTool/ScreenAnnotationTool.csproj
dotnet sln add tests/ScreenAnnotationTool.Tests/ScreenAnnotationTool.Tests.csproj
```

- [ ] **Step 4: Reference the app project from the test project**

```bash
dotnet add tests/ScreenAnnotationTool.Tests/ScreenAnnotationTool.Tests.csproj reference src/ScreenAnnotationTool/ScreenAnnotationTool.csproj
```

- [ ] **Step 5: Fix the test project target framework for WPF references**

The test project must target `net8.0-windows` and enable WPF so it can reference WPF types. Edit `tests/ScreenAnnotationTool.Tests/ScreenAnnotationTool.Tests.csproj` — set `<TargetFramework>net8.0-windows</TargetFramework>` and add `<UseWPF>true</UseWPF>` inside the existing `<PropertyGroup>`.

- [ ] **Step 6: Build the solution to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold WPF solution and test project"
```

---

### Task 2: AnnotationModel — tool/color/thickness state + annotation list

**Files:**
- Create: `src/ScreenAnnotationTool/Drawing/ToolKind.cs`
- Create: `src/ScreenAnnotationTool/Drawing/AnnotationModel.cs`
- Test: `tests/ScreenAnnotationTool.Tests/AnnotationModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum ToolKind { Freehand, Arrow, Rectangle, Ellipse, Text }`
  - `class AnnotationModel` with:
    - `ToolKind CurrentTool { get; set; }` (default `Freehand`)
    - `System.Windows.Media.Color CurrentColor { get; set; }` (default `Colors.Red`)
    - `double CurrentThickness { get; set; }` (default `4`)
    - `IReadOnlyList<object> Annotations { get; }`
    - `void Add(object annotation)`
    - `object? Undo()` — removes and returns last, or `null` if empty
    - `void Clear()`
    - `event EventHandler? Changed` — raised after Add/Undo/Clear

The annotation list holds `object` because the model is UI-free; the overlay stores WPF `UIElement`s through it. This keeps the model unit-testable without WPF UI dependencies (the WPF `Color` type lives in `PresentationCore` and is fine to reference).

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenAnnotationTool.Tests/AnnotationModelTests.cs`:
```csharp
using System.Windows.Media;
using ScreenAnnotationTool.Drawing;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class AnnotationModelTests
{
    [Fact]
    public void Defaults_AreFreehandRedThickness4()
    {
        var m = new AnnotationModel();
        Assert.Equal(ToolKind.Freehand, m.CurrentTool);
        Assert.Equal(Colors.Red, m.CurrentColor);
        Assert.Equal(4, m.CurrentThickness);
        Assert.Empty(m.Annotations);
    }

    [Fact]
    public void Add_AppendsAnnotation()
    {
        var m = new AnnotationModel();
        var item = new object();
        m.Add(item);
        Assert.Single(m.Annotations);
        Assert.Same(item, m.Annotations[0]);
    }

    [Fact]
    public void Undo_RemovesAndReturnsLast()
    {
        var m = new AnnotationModel();
        var a = new object();
        var b = new object();
        m.Add(a);
        m.Add(b);
        Assert.Same(b, m.Undo());
        Assert.Single(m.Annotations);
        Assert.Same(a, m.Annotations[0]);
    }

    [Fact]
    public void Undo_OnEmpty_ReturnsNull()
    {
        var m = new AnnotationModel();
        Assert.Null(m.Undo());
    }

    [Fact]
    public void Clear_EmptiesList()
    {
        var m = new AnnotationModel();
        m.Add(new object());
        m.Add(new object());
        m.Clear();
        Assert.Empty(m.Annotations);
    }

    [Fact]
    public void Changed_FiresOnAddUndoClear()
    {
        var m = new AnnotationModel();
        int count = 0;
        m.Changed += (_, _) => count++;
        m.Add(new object()); // 1
        m.Undo();            // 2
        m.Clear();           // 3
        Assert.Equal(3, count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AnnotationModelTests`
Expected: FAIL — `ToolKind` / `AnnotationModel` do not exist (compile errors).

- [ ] **Step 3: Implement ToolKind**

Create `src/ScreenAnnotationTool/Drawing/ToolKind.cs`:
```csharp
namespace ScreenAnnotationTool.Drawing;

public enum ToolKind
{
    Freehand,
    Arrow,
    Rectangle,
    Ellipse,
    Text
}
```

- [ ] **Step 4: Implement AnnotationModel**

Create `src/ScreenAnnotationTool/Drawing/AnnotationModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ScreenAnnotationTool.Drawing;

public class AnnotationModel
{
    private readonly List<object> _annotations = new();

    public ToolKind CurrentTool { get; set; } = ToolKind.Freehand;
    public Color CurrentColor { get; set; } = Colors.Red;
    public double CurrentThickness { get; set; } = 4;

    public IReadOnlyList<object> Annotations => _annotations;

    public event EventHandler? Changed;

    public void Add(object annotation)
    {
        _annotations.Add(annotation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public object? Undo()
    {
        if (_annotations.Count == 0)
            return null;
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
        return last;
    }

    public void Clear()
    {
        _annotations.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter AnnotationModelTests`
Expected: PASS — 6 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add AnnotationModel with tool/color/thickness state and undo/clear"
```

---

### Task 3: Win32 interop — window styles + monitor detection

**Files:**
- Create: `src/ScreenAnnotationTool/Interop/NativeMethods.cs`

**Interfaces:**
- Consumes: nothing.
- Produces static helpers in `static class NativeMethods`:
  - `void MakeClickThrough(IntPtr hwnd)` — adds `WS_EX_LAYERED | WS_EX_TRANSPARENT`.
  - `void MakeInteractive(IntPtr hwnd)` — keeps `WS_EX_LAYERED`, removes `WS_EX_TRANSPARENT`.
  - `System.Windows.Rect GetMonitorBoundsFromCursor()` — device-pixel bounds of the monitor under the cursor; primary monitor bounds on failure.
  - Constants `GWL_EXSTYLE`, `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`.

There is no unit test for this task — it is pure OS interop, verified when the overlay runs in Task 5. This task exists as its own commit because it is self-contained interop that Task 5 consumes.

- [ ] **Step 1: Implement NativeMethods**

Create `src/ScreenAnnotationTool/Interop/NativeMethods.cs`:
```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenAnnotationTool.Interop;

public static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static void MakeClickThrough(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    public static void MakeInteractive(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_LAYERED;
        ex &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    public static Rect GetMonitorBoundsFromCursor()
    {
        if (!GetCursorPos(out POINT pt))
            return SystemParameters.WorkArea;

        IntPtr mon = MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi))
            return SystemParameters.WorkArea;

        var r = mi.rcMonitor;
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add Win32 interop for click-through and monitor detection"
```

---

### Task 4: Shape factory — build WPF UIElements from drag geometry

**Files:**
- Create: `src/ScreenAnnotationTool/Drawing/ShapeFactory.cs`
- Test: `tests/ScreenAnnotationTool.Tests/ShapeFactoryTests.cs`

**Interfaces:**
- Consumes: `ToolKind`, WPF shape types.
- Produces `static class ShapeFactory` with:
  - `Rectangle CreateRectangle(Point start, Point end, Color color, double thickness)`
  - `Ellipse CreateEllipse(Point start, Point end, Color color, double thickness)`
  - `Path CreateArrow(Point start, Point end, Color color, double thickness)` — line plus arrowhead as a single `Path`
  - `Polyline StartFreehand(Point start, Color color, double thickness)` — a `Polyline` seeded with the first point; caller appends `Point`s to `.Points` during drag

Note: WPF `Shape`/`Rectangle`/`Ellipse`/`Path`/`Polyline` live in `System.Windows.Shapes`; `Path` geometry in `System.Windows.Media`. Rectangle/Ellipse position is set via `Canvas.SetLeft/SetTop` by the caller; the factory sets `Width`/`Height`. Arrow and freehand carry absolute coordinates in their geometry, so the caller places them at `(0,0)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenAnnotationTool.Tests/ShapeFactoryTests.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenAnnotationTool.Drawing;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class ShapeFactoryTests
{
    [WpfFact]
    public void CreateRectangle_NormalizesSizeAndPosition()
    {
        var r = ShapeFactory.CreateRectangle(new Point(30, 40), new Point(10, 20), Colors.Blue, 3);
        Assert.Equal(20, r.Width);
        Assert.Equal(20, r.Height);
        Assert.Equal(10, Canvas.GetLeft(r));
        Assert.Equal(20, Canvas.GetTop(r));
        Assert.Equal(3, r.StrokeThickness);
        Assert.Equal(Colors.Blue, ((SolidColorBrush)r.Stroke).Color);
    }

    [WpfFact]
    public void CreateEllipse_NormalizesSize()
    {
        var e = ShapeFactory.CreateEllipse(new Point(0, 0), new Point(50, 25), Colors.Green, 2);
        Assert.Equal(50, e.Width);
        Assert.Equal(25, e.Height);
        Assert.Equal(0, Canvas.GetLeft(e));
        Assert.Equal(0, Canvas.GetTop(e));
    }

    [WpfFact]
    public void CreateArrow_ReturnsPathWithColor()
    {
        var p = ShapeFactory.CreateArrow(new Point(0, 0), new Point(100, 0), Colors.Red, 4);
        Assert.NotNull(p.Data);
        Assert.Equal(4, p.StrokeThickness);
        Assert.Equal(Colors.Red, ((SolidColorBrush)p.Stroke).Color);
    }

    [WpfFact]
    public void StartFreehand_SeedsFirstPoint()
    {
        var pl = ShapeFactory.StartFreehand(new Point(5, 6), Colors.Black, 4);
        Assert.Single(pl.Points);
        Assert.Equal(new Point(5, 6), pl.Points[0]);
        Assert.Equal(4, pl.StrokeThickness);
    }
}
```

Note: these use `[WpfFact]` (an STA-thread fact) because they instantiate WPF UI objects. That attribute is added in the next step's package.

- [ ] **Step 2: Add the Xunit.StaFact package to the test project**

Run: `dotnet add tests/ScreenAnnotationTool.Tests/ScreenAnnotationTool.Tests.csproj package Xunit.StaFact`

Then add `using Xunit;` is already present; `WpfFact` comes from the `Xunit` namespace via StaFact. Verify by building next step.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter ShapeFactoryTests`
Expected: FAIL — `ShapeFactory` does not exist (compile errors).

- [ ] **Step 4: Implement ShapeFactory**

Create `src/ScreenAnnotationTool/Drawing/ShapeFactory.cs`:
```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenAnnotationTool.Drawing;

public static class ShapeFactory
{
    public static Rectangle CreateRectangle(Point start, Point end, Color color, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        var r = new Rectangle
        {
            Width = Math.Abs(end.X - start.X),
            Height = Math.Abs(end.Y - start.Y),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        return r;
    }

    public static Ellipse CreateEllipse(Point start, Point end, Color color, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        var e = new Ellipse
        {
            Width = Math.Abs(end.X - start.X),
            Height = Math.Abs(end.Y - start.Y),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        return e;
    }

    public static Path CreateArrow(Point start, Point end, Color color, double thickness)
    {
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(start, end));

        // Arrowhead
        double headLength = Math.Max(12, thickness * 4);
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double spread = Math.PI / 7;

        var p1 = new Point(
            end.X - headLength * Math.Cos(angle - spread),
            end.Y - headLength * Math.Sin(angle - spread));
        var p2 = new Point(
            end.X - headLength * Math.Cos(angle + spread),
            end.Y - headLength * Math.Sin(angle + spread));

        geometry.Children.Add(new LineGeometry(end, p1));
        geometry.Children.Add(new LineGeometry(end, p2));

        return new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }

    public static Polyline StartFreehand(Point start, Color color, double thickness)
    {
        var pl = new Polyline
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        pl.Points.Add(start);
        return pl;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter ShapeFactoryTests`
Expected: PASS — 4 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add ShapeFactory for rectangles, ellipses, arrows, freehand"
```

---

### Task 5: OverlayWindow — transparent canvas with click-through toggle + drawing

**Files:**
- Create: `src/ScreenAnnotationTool/Overlay/OverlayWindow.xaml`
- Create: `src/ScreenAnnotationTool/Overlay/OverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: `AnnotationModel`, `ToolKind`, `ShapeFactory`, `NativeMethods`.
- Produces `class OverlayWindow : Window` with:
  - constructor `OverlayWindow(AnnotationModel model)`
  - `void EnterDrawMode()` — position to cursor monitor, remove click-through, show/activate
  - `void ExitDrawMode()` — restore click-through so mouse passes through (window stays visible so annotations remain on screen)
  - `bool IsDrawing { get; }` — whether draw mode is active

Wiring detail: click-through must be applied whenever draw mode is off, including at first show. Apply it in the `SourceInitialized` handler (HWND exists there) and again in `ExitDrawMode`.

- [ ] **Step 1: Create the XAML**

Create `src/ScreenAnnotationTool/Overlay/OverlayWindow.xaml`:
```xml
<Window x:Class="ScreenAnnotationTool.Overlay.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        Title="Overlay">
    <Canvas x:Name="DrawCanvas" Background="Transparent"/>
</Window>
```

- [ ] **Step 2: Implement the code-behind**

Create `src/ScreenAnnotationTool/Overlay/OverlayWindow.xaml.cs`:
```csharp
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
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add OverlayWindow with click-through toggle and drawing tools"
```

---

### Task 6: SettingsService — persist toolbar position, color, thickness, startup pref

**Files:**
- Create: `src/ScreenAnnotationTool/Services/AppSettings.cs`
- Create: `src/ScreenAnnotationTool/Services/SettingsService.cs`
- Test: `tests/ScreenAnnotationTool.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `class AppSettings` with `double ToolbarLeft`, `double ToolbarTop`, `string ColorHex` (default `"#FFFF0000"`), `double Thickness` (default `4`), `bool LaunchOnStartup`.
  - `class SettingsService` with:
    - constructor `SettingsService(string filePath)` (production caller passes `%APPDATA%\ScreenAnnotationTool\settings.json`)
    - `AppSettings Load()` — returns defaults if file missing or unreadable
    - `void Save(AppSettings settings)` — creates directory if needed, writes JSON

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenAnnotationTool.Tests/SettingsServiceTests.cs`:
```csharp
using System.IO;
using ScreenAnnotationTool.Services;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class SettingsServiceTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "sat_test_" + Path.GetRandomFileName() + ".json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var svc = new SettingsService(TempFile());
        var s = svc.Load();
        Assert.Equal("#FFFF0000", s.ColorHex);
        Assert.Equal(4, s.Thickness);
        Assert.False(s.LaunchOnStartup);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        svc.Save(new AppSettings
        {
            ToolbarLeft = 100, ToolbarTop = 200,
            ColorHex = "#FF00FF00", Thickness = 8, LaunchOnStartup = true
        });

        var loaded = new SettingsService(path).Load();
        Assert.Equal(100, loaded.ToolbarLeft);
        Assert.Equal(200, loaded.ToolbarTop);
        Assert.Equal("#FF00FF00", loaded.ColorHex);
        Assert.Equal(8, loaded.Thickness);
        Assert.True(loaded.LaunchOnStartup);

        File.Delete(path);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ not valid json");
        var s = new SettingsService(path).Load();
        Assert.Equal("#FFFF0000", s.ColorHex);
        File.Delete(path);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SettingsServiceTests`
Expected: FAIL — `AppSettings` / `SettingsService` do not exist.

- [ ] **Step 3: Implement AppSettings**

Create `src/ScreenAnnotationTool/Services/AppSettings.cs`:
```csharp
namespace ScreenAnnotationTool.Services;

public class AppSettings
{
    public double ToolbarLeft { get; set; } = 40;
    public double ToolbarTop { get; set; } = 40;
    public string ColorHex { get; set; } = "#FFFF0000";
    public double Thickness { get; set; } = 4;
    public bool LaunchOnStartup { get; set; } = false;
}
```

- [ ] **Step 4: Implement SettingsService**

Create `src/ScreenAnnotationTool/Services/SettingsService.cs`:
```csharp
using System;
using System.IO;
using System.Text.Json;

namespace ScreenAnnotationTool.Services;

public class SettingsService
{
    private readonly string _filePath;

    public SettingsService(string filePath) => _filePath = filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter SettingsServiceTests`
Expected: PASS — 3 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add SettingsService with JSON persistence and safe defaults"
```

---

### Task 7: StartupService — toggle launch-on-startup via registry

**Files:**
- Create: `src/ScreenAnnotationTool/Services/StartupService.cs`
- Test: `tests/ScreenAnnotationTool.Tests/StartupServiceTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces `class StartupService`:
  - constructor `StartupService(Microsoft.Win32.RegistryKey rootKey, string subKeyPath, string valueName, string exePath)` — injectable for testing
  - `bool IsEnabled()` — true if the value exists and equals the exe path
  - `void SetEnabled(bool enabled)` — writes or removes the value
  - static factory `StartupService CreateDefault()` using `Registry.CurrentUser`, `Software\Microsoft\Windows\CurrentVersion\Run`, value `ScreenAnnotationTool`, and `Environment.ProcessPath`

Tests inject a throwaway subkey under `HKCU` (e.g. `Software\ScreenAnnotationTool_Test`) so they never touch the real Run key.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenAnnotationTool.Tests/StartupServiceTests.cs`:
```csharp
using Microsoft.Win32;
using ScreenAnnotationTool.Services;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class StartupServiceTests
{
    private const string TestSubKey = @"Software\ScreenAnnotationTool_Test";

    private static StartupService Make() =>
        new StartupService(Registry.CurrentUser, TestSubKey, "SAT_Test", @"C:\fake\app.exe");

    [Fact]
    public void Default_IsDisabled()
    {
        Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
        var svc = Make();
        Assert.False(svc.IsEnabled());
    }

    [Fact]
    public void Enable_ThenIsEnabledTrue()
    {
        var svc = Make();
        svc.SetEnabled(true);
        Assert.True(svc.IsEnabled());
        svc.SetEnabled(false);
        Assert.False(svc.IsEnabled());
        Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter StartupServiceTests`
Expected: FAIL — `StartupService` does not exist.

- [ ] **Step 3: Implement StartupService**

Create `src/ScreenAnnotationTool/Services/StartupService.cs`:
```csharp
using System;
using Microsoft.Win32;

namespace ScreenAnnotationTool.Services;

public class StartupService
{
    private readonly RegistryKey _rootKey;
    private readonly string _subKeyPath;
    private readonly string _valueName;
    private readonly string _exePath;

    public StartupService(RegistryKey rootKey, string subKeyPath, string valueName, string exePath)
    {
        _rootKey = rootKey;
        _subKeyPath = subKeyPath;
        _valueName = valueName;
        _exePath = exePath;
    }

    public static StartupService CreateDefault() =>
        new StartupService(
            Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            "ScreenAnnotationTool",
            Environment.ProcessPath ?? "");

    public bool IsEnabled()
    {
        using var key = _rootKey.OpenSubKey(_subKeyPath, writable: false);
        var value = key?.GetValue(_valueName) as string;
        return string.Equals(value, _exePath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = _rootKey.CreateSubKey(_subKeyPath, writable: true);
        if (key == null) return;
        if (enabled)
            key.SetValue(_valueName, _exePath);
        else if (key.GetValue(_valueName) != null)
            key.DeleteValue(_valueName);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter StartupServiceTests`
Expected: PASS — 2 passed.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add StartupService for launch-on-startup registry toggle"
```

---

### Task 8: HotkeyService — system-wide hotkey registration

**Files:**
- Create: `src/ScreenAnnotationTool/Services/HotkeyService.cs`

**Interfaces:**
- Consumes: nothing (WPF interop only).
- Produces `class HotkeyService : IDisposable`:
  - constructor `HotkeyService()` — creates a message-only `HwndSource` and hooks `WM_HOTKEY`
  - `void Register(int id, ModifierKeys modifiers, Key key)` — registers a global hotkey
  - `event Action<int>? HotkeyPressed` — raised with the id when a registered hotkey fires
  - `void Dispose()` — unregisters all and disposes the source
  - constants `int HotkeyToggleDraw = 1, HotkeyClear = 2` (Undo and Esc are handled inside the overlay via normal key events while it has focus, not as global hotkeys)

Rationale: only draw-toggle and clear need to work globally (app unfocused). Undo (Ctrl+Z) and Esc act only while the overlay is focused in draw mode, so they are handled as WPF `KeyDown` on the overlay, not global hotkeys — this avoids stealing Ctrl+Z from every other app.

- [ ] **Step 1: Implement HotkeyService**

Create `src/ScreenAnnotationTool/Services/HotkeyService.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenAnnotationTool.Services;

public class HotkeyService : IDisposable
{
    public const int HotkeyToggleDraw = 1;
    public const int HotkeyClear = 2;

    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly List<int> _ids = new();

    public event Action<int>? HotkeyPressed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("SAT_HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public void Register(int id, ModifierKeys modifiers, Key key)
    {
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        uint mods = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= 0x0008;

        if (RegisterHotKey(_source.Handle, id, mods, vk))
            _ids.Add(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke((int)wParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _ids)
            UnregisterHotKey(_source.Handle, id);
        _ids.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add HotkeyService for system-wide hotkey registration"
```

---

### Task 9: ToolbarWindow — floating tool/color/thickness bar

**Files:**
- Create: `src/ScreenAnnotationTool/Toolbar/ToolbarWindow.xaml`
- Create: `src/ScreenAnnotationTool/Toolbar/ToolbarWindow.xaml.cs`

**Interfaces:**
- Consumes: `AnnotationModel`, `ToolKind`, `StartupService`.
- Produces `class ToolbarWindow : Window`:
  - constructor `ToolbarWindow(AnnotationModel model, StartupService startup)`
  - `event Action? UndoRequested`
  - `event Action? ClearRequested`
  - `event Action<bool>? StartupToggled` (true = enable)
  - `event Action? CloseRequested`
  - `void SetDrawModeIndicator(bool on)` — updates the on/off visual
  - reads/writes `model.CurrentTool/CurrentColor/CurrentThickness` from the buttons

The toolbar is draggable by its background (`DragMove()` on mouse-down). Tool buttons set `model.CurrentTool`. Color swatches set `model.CurrentColor`. Thickness buttons set `model.CurrentThickness`. A checkbox reflects and toggles startup via the `StartupToggled` event.

- [ ] **Step 1: Create the XAML**

Create `src/ScreenAnnotationTool/Toolbar/ToolbarWindow.xaml`:
```xml
<Window x:Class="ScreenAnnotationTool.Toolbar.ToolbarWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="True"
        SizeToContent="WidthAndHeight"
        ResizeMode="NoResize"
        Title="Annotate">
    <Border x:Name="Root" Background="#EE2B2B2B" CornerRadius="10" Padding="8">
        <StackPanel Orientation="Horizontal">
            <StackPanel.Resources>
                <Style TargetType="Button">
                    <Setter Property="Width" Value="34"/>
                    <Setter Property="Height" Value="34"/>
                    <Setter Property="Margin" Value="2,0"/>
                    <Setter Property="Foreground" Value="White"/>
                    <Setter Property="Background" Value="#3C3C3C"/>
                    <Setter Property="BorderThickness" Value="0"/>
                    <Setter Property="FontSize" Value="16"/>
                </Style>
            </StackPanel.Resources>

            <Ellipse x:Name="DrawIndicator" Width="12" Height="12" Fill="Gray"
                     VerticalAlignment="Center" Margin="4,0,8,0"/>

            <Button x:Name="BtnFreehand" Content="✎" ToolTip="Freehand"/>
            <Button x:Name="BtnArrow" Content="↗" ToolTip="Arrow"/>
            <Button x:Name="BtnRect" Content="▭" ToolTip="Rectangle"/>
            <Button x:Name="BtnEllipse" Content="◯" ToolTip="Circle"/>
            <Button x:Name="BtnText" Content="T" ToolTip="Text"/>

            <Border Width="1" Background="#555" Margin="6,4"/>

            <Button x:Name="BtnRed" Background="Red" ToolTip="Red"/>
            <Button x:Name="BtnYellow" Background="Yellow" ToolTip="Yellow"/>
            <Button x:Name="BtnGreen" Background="LimeGreen" ToolTip="Green"/>
            <Button x:Name="BtnBlue" Background="DodgerBlue" ToolTip="Blue"/>
            <Button x:Name="BtnBlack" Background="Black" ToolTip="Black"/>

            <Border Width="1" Background="#555" Margin="6,4"/>

            <Button x:Name="BtnThin" Content="•" ToolTip="Thin"/>
            <Button x:Name="BtnMedium" Content="●" ToolTip="Medium"/>
            <Button x:Name="BtnThick" Content="⬤" ToolTip="Thick"/>

            <Border Width="1" Background="#555" Margin="6,4"/>

            <Button x:Name="BtnUndo" Content="⤺" ToolTip="Undo (Ctrl+Z)"/>
            <Button x:Name="BtnClear" Content="🗑" ToolTip="Clear all (Ctrl+Alt+X)"/>

            <Border Width="1" Background="#555" Margin="6,4"/>

            <CheckBox x:Name="ChkStartup" Content="Startup" Foreground="White"
                      VerticalAlignment="Center" Margin="4,0"/>
            <Button x:Name="BtnClose" Content="✕" ToolTip="Exit" Background="#772222"/>
        </StackPanel>
    </Border>
</Window>
```

- [ ] **Step 2: Implement the code-behind**

Create `src/ScreenAnnotationTool/Toolbar/ToolbarWindow.xaml.cs`:
```csharp
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
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add floating ToolbarWindow with tools, colors, thickness, startup toggle"
```

---

### Task 10: AppController + App wiring — bring it all together

**Files:**
- Create: `src/ScreenAnnotationTool/AppController.cs`
- Modify: `src/ScreenAnnotationTool/App.xaml`
- Modify: `src/ScreenAnnotationTool/App.xaml.cs`
- Delete: `src/ScreenAnnotationTool/MainWindow.xaml` and `MainWindow.xaml.cs` (created by the wpf template; unused)

**Interfaces:**
- Consumes: `AnnotationModel`, `OverlayWindow`, `ToolbarWindow`, `HotkeyService`, `StartupService`, `SettingsService`, `AppSettings`, `NativeMethods`.
- Produces `class AppController` with:
  - constructor `AppController()` — builds all services + windows, wires events
  - `void Start()` — loads settings, shows toolbar, registers hotkeys
  - `void Shutdown()` — saves settings, disposes hotkeys, closes windows

Behavior wired here:
- Toggle-draw hotkey → if overlay not drawing, `overlay.EnterDrawMode()` + indicator on; else `overlay.ExitDrawMode()` + indicator off.
- Clear hotkey and toolbar Clear → `model.Clear()`.
- Toolbar Undo → `model.Undo()`.
- Overlay `KeyDown`: Esc → exit draw mode; Ctrl+Z → `model.Undo()`.
- Toolbar StartupToggled → `startup.SetEnabled(value)`.
- Toolbar CloseRequested → `Shutdown()` then `Application.Current.Shutdown()`.
- Settings: restore toolbar position + color + thickness on start; save on shutdown.

- [ ] **Step 1: Implement AppController**

Create `src/ScreenAnnotationTool/AppController.cs`:
```csharp
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
```

- [ ] **Step 2: Delete the unused template MainWindow files**

```bash
rm src/ScreenAnnotationTool/MainWindow.xaml src/ScreenAnnotationTool/MainWindow.xaml.cs
```

- [ ] **Step 3: Update App.xaml to remove the StartupUri**

Replace the contents of `src/ScreenAnnotationTool/App.xaml` with (note: no `StartupUri` attribute):
```xml
<Application x:Class="ScreenAnnotationTool.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Update App.xaml.cs to launch the controller**

Replace the contents of `src/ScreenAnnotationTool/App.xaml.cs` with:
```csharp
using System.Windows;

namespace ScreenAnnotationTool;

public partial class App : Application
{
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _controller = new AppController();
        _controller.Start();
    }
}
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all tests from Tasks 2, 4, 6, 7 pass (15 tests).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: wire AppController, hotkeys, overlay, toolbar into running app"
```

---

### Task 11: Manual run verification + README

**Files:**
- Create: `README.md`

**Interfaces:**
- Consumes: the built app.
- Produces: a verified running app and usage docs. No new code interfaces.

- [ ] **Step 1: Launch the app**

Run: `dotnet run --project src/ScreenAnnotationTool`
Expected: the floating toolbar appears; no main window; the app stays running.

- [ ] **Step 2: Verify each behavior manually**

Confirm each on-screen (this is the acceptance checklist from the spec):
- Press **Ctrl+Alt+D** — draw indicator turns green; overlay captures the mouse on the monitor under the cursor.
- Freehand: drag to draw a red line.
- Arrow / Rectangle / Circle: pick each tool, drag, confirm the shape commits on mouse-up with a live preview during drag.
- Text: pick Text, click, type, press Enter — text commits.
- Color buttons change subsequent stroke color; thickness buttons change width.
- **Ctrl+Z** (overlay focused) removes the last annotation; toolbar Undo does the same.
- **Ctrl+Alt+X** clears everything; toolbar Clear does the same.
- Press **Ctrl+Alt+D** again (or **Esc**) — indicator goes gray; clicks now pass through to apps underneath while drawn shapes stay visible.
- Toggle **Startup** checkbox on, close and reopen the app — checkbox remains checked (registry persisted).
- Drag the toolbar, close via **✕**, relaunch — toolbar reappears at its last position (settings persisted).

Fix any defect found before proceeding. Re-run `dotnet build` and `dotnet test` after any fix.

- [ ] **Step 3: Write the README**

Create `README.md`:
```markdown
# Screen Annotation Tool

A lightweight Windows overlay for drawing on your screen during tutorial recordings.

## Requirements
- Windows 11
- .NET 8 SDK (to build) — `dotnet run --project src/ScreenAnnotationTool`

## Hotkeys
- **Ctrl + Alt + D** — toggle draw mode on/off
- **Ctrl + Alt + X** — clear all annotations
- **Ctrl + Z** — undo last annotation (while drawing)
- **Esc** — exit draw mode

## Tools
Freehand, arrow, rectangle, circle, and text — with color and thickness pickers,
undo, and clear-all, all on a floating toolbar you can drag anywhere.

## Startup
Tick the **Startup** checkbox on the toolbar to launch the app automatically
when Windows starts.

## Build a standalone exe
```
dotnet publish src/ScreenAnnotationTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```
The exe lands in `src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish/`.
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: add README; verify app end-to-end"
```

---

## Self-Review Notes

**Spec coverage:** OverlayWindow + click-through (Task 3, 5) ✓; ToolbarWindow with all controls (Task 9) ✓; HotkeyService (Task 8) ✓; StartupService (Task 7) ✓; AnnotationModel add/undo/clear (Task 2) ✓; all five drawing tools (Task 4, 5) ✓; color + thickness (Task 4, 5, 9) ✓; monitor-follow (Task 3, 5) ✓; settings persistence (Task 6) ✓; startup toggle (Task 7, 9, 10) ✓; error handling — settings/registry/monitor fallbacks (Tasks 3, 6, 7) ✓; hotkey-registration failure handled by `RegisterHotKey` bool check without throwing (Task 8) ✓; testing approach — unit tests for model/factory/settings/startup + manual run verification (all tasks + Task 11) ✓.

**Type consistency:** `AnnotationModel` members (`CurrentTool`, `CurrentColor`, `CurrentThickness`, `Add`, `Undo`, `Clear`, `Changed`, `Annotations`) used identically across Tasks 5, 9, 10 ✓. `ToolKind` values consistent ✓. `NativeMethods.MakeClickThrough/MakeInteractive/GetMonitorBoundsFromCursor` consistent between Tasks 3 and 5 ✓. `HotkeyService.HotkeyToggleDraw/HotkeyClear/Register/HotkeyPressed` consistent between Tasks 8 and 10 ✓. `StartupService` ctor + `IsEnabled/SetEnabled/CreateDefault` consistent between Tasks 7, 9, 10 ✓. `ToolbarWindow` events (`UndoRequested/ClearRequested/StartupToggled/CloseRequested/SetDrawModeIndicator`) consistent between Tasks 9 and 10 ✓.

**Placeholder scan:** no TBD/TODO; every code step shows complete code ✓.
