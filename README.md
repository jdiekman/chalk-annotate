# Screen Annotation Tool

A lightweight Windows overlay for drawing on your screen during tutorial recordings —
point things out with arrows, boxes, freehand ink, and text, then clear it in one keystroke.

Built for live YouTube tutorials: toggle a transparent draw layer over whatever app you're
demoing, mark it up, and toggle back to normal clicks without closing anything.

## Requirements

- Windows 10/11
- .NET 8 SDK (to build and run from source)

## Run from source

```
dotnet run --project src/ScreenAnnotationTool
```

A small floating toolbar appears (titled **Annotate**). There is no main window — the app
lives in the toolbar and a full-screen transparent overlay that only captures the mouse while
draw mode is on.

## Hotkeys

| Hotkey | Action |
|--------|--------|
| **Ctrl + Alt + D** | Toggle draw mode on/off |
| **Ctrl + Alt + X** | Clear all annotations |
| **Ctrl + Z** | Undo the last annotation (while drawing) |
| **Esc** | Exit draw mode |

When draw mode is **on**, the green dot on the toolbar lights up and the overlay captures your
mouse so you can draw; the selected tool button shows a blue highlight. When you toggle draw
mode **off** (Ctrl+Alt+D again, or Esc), the annotations are cleared and your mouse returns to
working normally — clicks pass straight through to the app underneath. Use **Ctrl+Alt+X** to
wipe the screen without leaving draw mode. The overlay appears on whichever monitor your cursor
is on when you toggle it on, and the floating toolbar always stays above it so its buttons are
reachable while you draw.

## Tools

Freehand, arrow, rectangle, and circle, plus text — with a color palette (red / yellow /
green / blue / black) and three stroke thicknesses. Undo and Clear-all are on the toolbar too.
Drag the toolbar anywhere by its background; it remembers its position, last color, and
thickness between sessions.

## Launch on Windows startup

Tick the **Startup** checkbox on the toolbar to have the app launch automatically when Windows
starts. Untick it to remove it. (Implemented via the per-user
`HKCU\...\CurrentVersion\Run` registry key — no admin rights needed.)

## Build a standalone .exe

```
dotnet publish src/ScreenAnnotationTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The single-file exe lands in
`src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish/`. Copy it anywhere and
run it — no .NET install required on the target machine. Put a shortcut in your Startup folder,
or use the Startup checkbox, to have it always ready when you record.

## How it works

- **OverlayWindow** — a borderless, transparent, top-most window sized to the active monitor.
  Win32 extended window styles (`WS_EX_TRANSPARENT`) toggle click-through on/off. Declared
  Per-Monitor-V2 DPI-aware so it lands correctly on displays at any scaling.
- **ToolbarWindow** — the floating control bar.
- **HotkeyService** — registers the global hotkeys via `RegisterHotKey` so they work even when
  the app isn't focused.
- **AnnotationModel / ShapeFactory** — hold the annotation list (undo/clear) and build the WPF
  shapes. These have unit tests (`dotnet test`).

## Tests

```
dotnet test
```

Covers the annotation model, shape factory, settings persistence, and startup toggle. The
overlay, toolbar, and hotkeys are interactive UI and are verified by running the app.
