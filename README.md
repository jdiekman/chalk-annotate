# Chalk

A lightweight Windows overlay for drawing on your screen — point things out with arrows,
boxes, freehand ink, and text during tutorial videos, screen shares, and presentations.

Chalk lives in your system tray and out of the way until you need it. Hit a hotkey and a
transparent draw layer appears over whatever you're doing; mark it up, then toggle back to
normal — no window juggling, nothing to alt-tab.

## Install

Grab **ChalkSetup-\<version\>.exe** from the
[latest release](https://github.com/jdiekman/chalk-annotate/releases/latest) and run it —
no admin rights needed. The installer offers a desktop shortcut, a
"start Chalk when Windows starts" option, and offers to launch Chalk when it finishes
(look for the chalk icon in your system tray).

Prefer no installer? **Chalk-\<version\>-portable.exe** on the same page is a single
standalone exe — copy it anywhere and run it.

> **Windows SmartScreen:** Check the release notes and Authenticode signature before
> bypassing any warning. Every release includes `SHA256SUMS.txt` and a GitHub build-provenance
> attestation so downloaded artifacts can be verified independently.

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
- **Easy install** — a small setup wizard, or a single portable `.exe` if you prefer;
  either way, no .NET runtime to install on the target machine.

## Hotkeys

| Hotkey | Action |
|--------|--------|
| **Ctrl + Alt + Z** | Start/stop drawing (shows the toolbar if hidden) |
| **Ctrl + Alt + X** | Clear all annotations |
| **Ctrl + Z** | Undo the last annotation (while drawing) |
| **Esc** | Stop drawing |

The round dot on the left of the toolbar does the same thing as **Ctrl + Alt + Z** — click it
to start/stop drawing; it glows green while drawing.

## How it works

- **Start it** and it sits in the system tray (nothing on screen).
- **Ctrl + Alt + Z** shows the floating toolbar and drops you straight into draw mode. Draw
  with the mouse; pick a tool, color, and thickness from the toolbar.
- **Ctrl + Alt + Z** again (or **Esc**, or the toolbar dot) stops drawing and clears the marks;
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

To build the installer too, install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup`) and run `./build-installer.ps1` — artifacts land in
`dist/`. Releases are built automatically by GitHub Actions when a `v*` tag is pushed.

## Tests

```
dotnet test
```

Covers the annotation model, shape factory, settings validation and persistence, startup
registration, and defensive native-call validation. Full overlay, tray, global-hotkey, and
mixed-DPI behavior is also verified interactively on Windows.

## Release security

Release dependencies are restored from committed lock files, GitHub Actions are pinned to
reviewed commits, and release executables receive SHA-256 checksums and GitHub build-provenance
attestations. `build-installer.ps1` signs both `Chalk.exe` and the installer when these repository
secrets are configured:

- `CHALK_SIGNING_CERTIFICATE_BASE64` — a base64-encoded Authenticode `.pfx` certificate.
- `CHALK_SIGNING_CERTIFICATE_PASSWORD` — the certificate password.

Local builds without those environment variables remain possible but print an explicit unsigned
artifact warning. Never add the certificate or its password to the repository.

## Tech

C# / .NET 8, WPF for the overlay and toolbar, a WinForms `NotifyIcon` for the tray, and Win32
interop for the click-through overlay, global hotkeys, per-monitor DPI, and the startup
registry entry.

## License

[MIT](LICENSE) © James Diekman
