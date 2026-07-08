# Chalk

A lightweight Windows overlay for drawing on your screen — point things out with arrows,
boxes, freehand ink, and text during tutorial videos, screen shares, and presentations.

Chalk lives in your system tray and out of the way until you need it. Hit a hotkey and a
transparent draw layer appears over whatever you're doing; mark it up, then toggle back to
normal — no window juggling, nothing to alt-tab.

## Features

- **Hotkey-driven** — summon and dismiss the draw layer with a global shortcut, from any app.
- **Tools** — freehand, arrow, rectangle, circle, and text.
- **Colors** — a pop-up palette of 5 color families (red, yellow, green, blue, grayscale)
  with 5 light→dark shades each.
- **Stroke thickness** — thin / medium / thick.
- **Undo & clear** — undo the last mark, or wipe the screen instantly.
- **Draw over anything** — the overlay follows the monitor your cursor is on and is
  DPI-correct at any display scaling.
- **System tray** — starts hidden; a tray icon gives you Show / Exit at any time.
- **Launch on startup** — optional, so it's always ready when you record.
- Single self-contained `.exe`, no installer, no runtime to install on the target machine.

## Hotkeys

| Hotkey | Action |
|--------|--------|
| **Ctrl + Alt + D** | Start/stop drawing (shows the toolbar if hidden) |
| **Ctrl + Alt + X** | Clear all annotations |
| **Ctrl + Z** | Undo the last annotation (while drawing) |
| **Esc** | Stop drawing |

The round dot on the left of the toolbar does the same thing as **Ctrl + Alt + D** — click it
to start/stop drawing; it glows green while drawing.

## How it works

- **Start it** and it sits in the system tray (nothing on screen).
- **Ctrl + Alt + D** shows the floating toolbar and drops you straight into draw mode. Draw
  with the mouse; pick a tool, color, and thickness from the toolbar.
- **Ctrl + Alt + D** again (or **Esc**, or the toolbar dot) stops drawing and clears the marks;
  your mouse returns to normal and clicks pass through to the app underneath.
- The **✕** button hides the toolbar back to the tray (the app keeps running). Bring it back
  with the hotkey or by clicking the tray icon.
- **Exit** the app from the tray icon's right-click menu.

The toolbar remembers its position, color, and thickness between sessions, and the **Startup**
checkbox toggles launching Chalk automatically when Windows starts.

## Run from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet run --project src/ScreenAnnotationTool
```

## Build a standalone .exe

```
dotnet publish src/ScreenAnnotationTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The single-file `Chalk.exe` lands in
`src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish/`. Copy it anywhere and
run it — no .NET install required on the target machine. Tick the **Startup** checkbox (or drop
a shortcut in your Startup folder) to have it ready whenever you record.

## Tests

```
dotnet test
```

Covers the annotation model, shape factory, settings persistence, and startup toggle. The
overlay, toolbar, tray, and global hotkeys are interactive and verified by running the app.

## Tech

C# / .NET 8, WPF for the overlay and toolbar, a WinForms `NotifyIcon` for the tray, and Win32
interop for the click-through overlay, global hotkeys, per-monitor DPI, and the startup
registry entry.

## License

[MIT](LICENSE) © James Diekman
