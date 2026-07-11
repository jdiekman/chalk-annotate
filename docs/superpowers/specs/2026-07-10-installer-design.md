# Chalk Installer & Release Pipeline — Design

Date: 2026-07-10
Status: Approved

## Goal

Package Chalk into a Windows installer that general-public users (YouTube tutorial
viewers) can download from GitHub Releases and install with a double-click, plus an
automated release pipeline that produces it. The existing portable single-file
`Chalk.exe` remains available alongside the installer.

## Decisions (from brainstorming)

- **Audience:** general public via GitHub Releases linked from YouTube descriptions.
- **Code signing:** ship unsigned for now. Document the SmartScreen "More info →
  Run anyway" flow in the README. Signing (e.g. Azure Trusted Signing) can be added
  later without changing the design.
- **Updates:** manual — users download the new installer from Releases; it upgrades
  in place. No auto-update framework.
- **Release builds:** GitHub Actions on version tags (`v*`), plus the same build
  script runnable locally.
- **Install options:** per-user install (no admin prompt), always a Start Menu
  shortcut, optional desktop shortcut, optional "start Chalk when Windows starts"
  task, and a "Launch Chalk" checkbox on the finish page.
- **Technology:** Inno Setup 6 (free, single readable script, per-user installs,
  preinstalled on GitHub Actions Windows runners). WiX/MSI rejected as
  enterprise-oriented overkill; MSIX rejected because it requires signing.

## Components

### 1. Versioning — csproj is the single source of truth

Add `<Version>` (starting at `1.0.0`) to `src/ScreenAnnotationTool/ScreenAnnotationTool.csproj`.
The exe's file/product version, the installer filename, and the Add/Remove Programs
entry all derive from it. The build script reads it from the csproj (simple XML/regex
read) and passes it to Inno Setup via `/D` define.

### 2. Single-instance mutex in the app

Already implemented: `App.OnStartup` creates a named mutex
`Chalk_SingleInstance_9f1c` (src/ScreenAnnotationTool/App.xaml.cs) that is held for
the app's lifetime. No app change needed — the installer's `AppMutex` directive
references this existing name to detect a running Chalk and prompt the user to
close it before upgrading (files can't be replaced while the exe runs).

### 3. Installer script — `installer/chalk.iss`

Inno Setup 6 script. Key directives:

- `AppId` — fixed GUID so upgrades replace rather than duplicate the install.
- `PrivilegesRequired=lowest` — per-user, no UAC prompt.
- `DefaultDirName={localappdata}\Programs\Chalk`.
- `AppMutex=Chalk_SingleInstance_9f1c` — close-before-upgrade prompt.
- Version, app name, publisher, URL, and license (MIT, shown as info page — not a
  click-through EULA) fed from `/D` defines / repo files.
- `SetupIconFile` — `chalk.ico`; installer and uninstaller use the app icon.
- **Files:** the published single-file `Chalk.exe` only.
- **Icons:** Start Menu shortcut always; desktop shortcut behind an unchecked-by-
  default task.
- **Startup task:** checked-by-default task "Start Chalk when Windows starts" that
  writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `Chalk`,
  data = installed exe path — identical key/value-name to `StartupService.CreateDefault()`,
  so the toolbar's Startup checkbox reflects and can toggle what the installer set.
  The path must be written **unquoted** (`{app}\Chalk.exe`, no surrounding quotes):
  `StartupService.IsEnabled()` compares the value to `Environment.ProcessPath` with
  a plain string equality, and a quoted value would never match. (The path contains
  no user-controlled segments beyond the profile dir; unquoted is safe here and is
  what `StartupService.SetEnabled` itself writes.)
  Flag: `uninsdeletevalue` so uninstall removes it.
- **Run section:** "Launch Chalk" checkbox (checked) on the finish page,
  `postinstall nowait`.
- **Uninstall:** removes the app folder, shortcuts, and Run entry. Settings under
  `%APPDATA%` are left in place (cheap, and harmless if the user reinstalls);
  uninstaller does not prompt.

### 4. Build script — `build-installer.ps1` (repo root)

One script used locally and in CI:

1. Read `<Version>` from the csproj.
2. `dotnet publish src/ScreenAnnotationTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
3. Locate ISCC.exe (standard install path or `ISCC` on PATH), fail with a clear
   message if missing.
4. Compile `installer/chalk.iss` with `/DAppVersion=<version> /DPublishDir=<path>`.
5. Emit `dist/ChalkSetup-<version>.exe` and copy the portable `Chalk.exe` to
   `dist/Chalk-<version>-portable.exe`.

`dist/` is gitignored.

### 5. CI — `.github/workflows/release.yml`

Trigger: push of tag matching `v*`. Runner: `windows-latest` (has .NET 8 SDK and
Inno Setup preinstalled; the script's ISCC lookup covers the runner's install path).

Steps: checkout → `dotnet test` → `./build-installer.ps1` → verify the tag version
matches the csproj version (fail the release on mismatch) → create GitHub Release
with `ChalkSetup-<version>.exe` and `Chalk-<version>-portable.exe` attached,
auto-generated release notes.

### 6. README update

Replace the lead of the distribution story: new **Install** section at the top
pointing at the Releases page, with the SmartScreen note ("More info → Run anyway"
until the app builds download reputation). Keep the portable-exe and
build-from-source sections. Remove the now-outdated "no installer" bullet from
Features.

## Error handling

- **Chalk running during install/upgrade:** Inno's `AppMutex` prompt asks the user
  to close it; install proceeds once closed.
- **ISCC not found locally:** build script exits with instructions to install Inno
  Setup 6 (winget id `JRSoftware.InnoSetup`).
- **Tag/csproj version mismatch in CI:** release job fails before publishing.
- **Startup registry write:** handled by Inno natively; per-user key, no elevation
  needed.

## Testing

- Unit tests unchanged; CI runs `dotnet test` before packaging.
- Manual verification checklist (release-time, on a machine or VM):
  1. Install fresh → app in Add/Remove Programs, Start Menu shortcut works, tray
     icon appears when launched from finish page.
  2. Startup task checked → `Chalk` value present under HKCU Run; toolbar Startup
     checkbox shows checked.
  3. Run installer again (same or newer version) while Chalk is running → prompted
     to close → upgrade succeeds, single Add/Remove entry.
  4. Uninstall → exe, shortcuts, and Run entry gone; settings folder remains.
  5. Portable exe still runs standalone.

## Out of scope

- Code signing (documented follow-up; slot into `build-installer.ps1` later).
- Auto-update, winget publication, ARM64/x86 builds, localization.
