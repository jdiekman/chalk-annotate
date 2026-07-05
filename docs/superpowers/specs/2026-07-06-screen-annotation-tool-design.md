# Screen Annotation Tool — Design Spec

**Date:** 2026-07-06
**Platform:** Windows 11
**Stack:** C# / .NET 8 (WPF)
**Purpose:** A personal, hotkey-triggered screen-annotation overlay for pointing things
out during YouTube tutorial recordings. Similar in spirit to Epic Pen / ZoomIt.

---

## Goals

- Toggle a transparent, always-on-top drawing overlay with a global hotkey.
- Draw rectangles, circles, arrows, freehand strokes, and text directly on the screen.
- Pick pen color and stroke thickness from a floating toolbar.
- Undo the last annotation and clear everything quickly, mid-recording.
- Overlay appears on whichever monitor currently holds the cursor.
- Optionally launch the app on Windows startup.

## Non-Goals (v1)

Architected to allow these later, but **not built now**:

- Multi-monitor spanning (single active monitor only).
- Spotlight / highlighter / screen-dimming.
- ZoomIt-style live screen magnification.
- Saving or exporting annotations to a file.
- Rebindable hotkey UI (hotkeys are fixed defaults in v1, defined in one place).

---

## Architecture

Three cooperating components plus a shared drawing model.

### 1. OverlayWindow

A borderless, fully transparent, topmost WPF window sized to one monitor's bounds.

- **Draw mode OFF:** the window is *click-through*. Extended window styles
  `WS_EX_TRANSPARENT | WS_EX_LAYERED` are set via P/Invoke (`GetWindowLong` /
  `SetWindowLong`), so mouse input passes through to the apps underneath. Any
  previously drawn annotations remain visible on top.
- **Draw mode ON:** the `WS_EX_TRANSPARENT` flag is removed so the window captures
  mouse input for drawing. On toggle-on, the overlay repositions and resizes to the
  monitor that currently contains the cursor (via `Cursor.Position` +
  `Screen.FromPoint`, or Win32 `MonitorFromPoint`).
- Hosts a `Canvas` that is the drawing surface.
- Window chrome: `WindowStyle=None`, `AllowsTransparency=True`,
  `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`.

### 2. ToolbarWindow

A small, draggable, always-on-top floating window. Never click-through.

Controls:

- Tool buttons: **Freehand**, **Arrow**, **Rectangle**, **Ellipse (circle)**, **Text**.
- **Color picker** — a small palette of common colors plus a custom-color option.
- **Thickness selector** — a few preset stroke widths (e.g. 2 / 4 / 8 px).
- **Undo** button.
- **Clear all** button.
- **Draw-mode indicator** — shows whether draw mode is currently on/off.
- **Settings / overflow** — includes the "Launch on Windows startup" checkbox.
- **Close** button (exits the app).

Behavior:

- Draggable by clicking anywhere on its background.
- Remembers its screen position between sessions (persisted in settings).
- Stays visible whether or not draw mode is active, so tools/colors can be pre-selected.

### 3. HotkeyService

Registers system-wide hotkeys with Windows so they fire even when the app is not
focused.

- Uses Win32 `RegisterHotKey` / `UnregisterHotKey`, bound to a hidden `HwndSource`
  message loop (or the toolbar window's HWND) that listens for `WM_HOTKEY`.
- Fixed default bindings (v1):
  - **Ctrl + Alt + D** — toggle draw mode on/off
  - **Ctrl + Alt + X** — clear all annotations
  - **Ctrl + Z** — undo last annotation (while draw mode is active)
  - **Esc** — exit draw mode
- All bindings defined in a single constants location so a future settings UI can
  make them rebindable.

### 4. StartupService

Toggles "launch on Windows startup".

- Writes/removes a value under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the app's exe path.
- State reflected by the toolbar's startup checkbox and read on launch.

---

## Drawing Model

The overlay's `Canvas` displays annotations, each a WPF `UIElement`:

| Tool       | Element                                   | Interaction                                           |
|------------|-------------------------------------------|-------------------------------------------------------|
| Freehand   | `Polyline`                                | Points collected on mouse-move during drag            |
| Arrow      | `Line` + polygon arrowhead (grouped)      | Drag from start to end; arrowhead drawn at end point  |
| Rectangle  | `Rectangle`                               | Drag defines opposite corners                         |
| Circle     | `Ellipse`                                 | Drag defines bounding box                             |
| Text       | Editable `TextBox` → committed `TextBlock`| Click places caret; Enter or click-away commits       |

### Annotation state

A plain-C# model class (no WPF UI dependency) owns the ordered list of annotations
and the current tool/color/thickness. It exposes:

- `AddAnnotation(item)` — append.
- `Undo()` — remove and return the last item (no-op if empty).
- `Clear()` — empty the list.
- Current-tool / current-color / current-thickness properties.

This class is unit-testable without a running UI. The overlay observes it and adds/
removes the corresponding `UIElement`s on the `Canvas`.

### Drawing interaction (draw mode ON)

- **Shape tools (arrow/rect/circle):** mouse-down records the start point; mouse-move
  shows a live "rubber-band" preview; mouse-up commits the final shape.
- **Freehand:** mouse-down starts a `Polyline`; each mouse-move appends a point;
  mouse-up commits.
- **Text:** mouse-down places an editable `TextBox` at that point; commit on Enter or
  focus-loss converts it to a static `TextBlock`.

---

## Data Flow

```
Global hotkey / toolbar click
        │
        ▼
   AppController  ──(sets tool/color/thickness, toggles draw mode)──►  AnnotationModel
        │                                                                  │
        ▼                                                                  ▼
  OverlayWindow  ◄──(observes model, renders UIElements on Canvas)────────┘
   (click-through toggled via P/Invoke based on draw mode)
```

- Toolbar and hotkeys both funnel through a single `AppController` that owns app state
  and the `AnnotationModel`, so there is one source of truth.

---

## Persistence

A small settings file (JSON in `%APPDATA%\ScreenAnnotationTool\settings.json`) stores:

- Toolbar screen position.
- Last-used color and thickness.
- "Launch on startup" preference (the registry entry is the source of truth; the
  setting mirrors it for the checkbox).

---

## Error Handling

- **Hotkey registration failure** (another app owns the combo): catch, log, and show a
  brief toolbar notice; the app still runs with toolbar buttons.
- **Registry write failure** (startup toggle): catch, surface a message, leave the
  checkbox reflecting actual state.
- **Monitor detection edge cases:** fall back to the primary monitor if the cursor's
  monitor can't be resolved.

---

## Testing Approach

- **Unit tests:** `AnnotationModel` (add / undo / clear / tool-state transitions) —
  pure C#, no UI.
- **Manual / run verification:** the Win32 overlay, click-through toggling, global
  hotkeys, monitor-follow, and each drawing tool are verified by launching the app and
  confirming behavior on-screen (draw toggle, freehand, arrow, rectangle, circle, text,
  color, thickness, undo, clear, startup toggle) before the work is considered done.

---

## Project Structure (proposed)

```
ScreenAnnotationTool/
  ScreenAnnotationTool.sln
  src/
    ScreenAnnotationTool/            # WPF app
      App.xaml / App.xaml.cs
      AppController.cs
      Overlay/OverlayWindow.xaml(.cs)
      Toolbar/ToolbarWindow.xaml(.cs)
      Drawing/AnnotationModel.cs
      Drawing/Tools.cs               # tool enum + shape factory helpers
      Services/HotkeyService.cs
      Services/StartupService.cs
      Services/SettingsService.cs
      Interop/NativeMethods.cs       # P/Invoke declarations
  tests/
    ScreenAnnotationTool.Tests/      # xUnit tests for AnnotationModel
```
