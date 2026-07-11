# Chalk Installer & Release Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Package Chalk into an unsigned per-user Inno Setup installer published to GitHub Releases by a tag-triggered workflow, alongside the existing portable exe.

**Architecture:** A single Inno Setup script (`installer/chalk.iss`) consumes the already-supported single-file publish output. One PowerShell build script drives publish + installer compile identically on a dev machine and in CI. A GitHub Actions workflow runs tests, builds, verifies the tag matches the csproj version, and attaches both artifacts to a Release.

**Tech Stack:** .NET 8 SDK (`dotnet publish`), Inno Setup 6 (ISCC), PowerShell 7, GitHub Actions (`windows-latest`), softprops/action-gh-release.

**Spec:** `docs/superpowers/specs/2026-07-10-installer-design.md`

## Global Constraints

- Install is per-user: `PrivilegesRequired=lowest`, install dir `{localappdata}\Programs\Chalk`, no UAC prompt anywhere.
- The startup registry value MUST be written **unquoted**: key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `Chalk`, data `{app}\Chalk.exe` with no surrounding quotes — `StartupService.IsEnabled()` compares it to `Environment.ProcessPath` with plain string equality.
- The app's existing mutex name is `Chalk_SingleInstance_9f1c` (src/ScreenAnnotationTool/App.xaml.cs:13). Do not rename it; the installer references it verbatim.
- Version single source of truth: `<Version>` in `src/ScreenAnnotationTool/ScreenAnnotationTool.csproj`, starting at `1.0.0`.
- Product name is **Chalk**, publisher **James Diekman**, repo URL `https://github.com/jdiekman/chalk-annotate`.
- No code signing, no auto-update, win-x64 only.
- Windows-only work; all script steps run in PowerShell 7 (`pwsh`).

---

### Task 1: Version in csproj

**Files:**
- Modify: `src/ScreenAnnotationTool/ScreenAnnotationTool.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: `<Version>1.0.0</Version>` inside the first `<PropertyGroup>`. Task 2's build script reads it via XML as `Project.PropertyGroup.Version`; Task 3's workflow reads it the same way.

- [ ] **Step 1: Add the Version property**

In `src/ScreenAnnotationTool/ScreenAnnotationTool.csproj`, add one line after `<Product>Chalk</Product>`:

```xml
    <Product>Chalk</Product>
    <Version>1.0.0</Version>
```

- [ ] **Step 2: Verify the built exe carries the version**

Run:
```powershell
dotnet build src/ScreenAnnotationTool -c Release
(Get-Item src/ScreenAnnotationTool/bin/Release/net8.0-windows/Chalk.exe).VersionInfo.ProductVersion
```
Expected: build succeeds; output is `1.0.0`.

- [ ] **Step 3: Verify tests still pass**

Run: `dotnet test`
Expected: all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/ScreenAnnotationTool/ScreenAnnotationTool.csproj
git commit -m "chore: add Version 1.0.0 to csproj as release version source of truth"
```

---

### Task 2: Inno Setup script + build script

**Files:**
- Create: `installer/chalk.iss`
- Create: `build-installer.ps1` (repo root)
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `<Version>` from Task 1; publish output `src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish/Chalk.exe`; app mutex name `Chalk_SingleInstance_9f1c`; icon `src/ScreenAnnotationTool/chalk.ico`; `LICENSE` at repo root.
- Produces: `dist/ChalkSetup-<version>.exe` and `dist/Chalk-<version>-portable.exe`. Running `./build-installer.ps1` from the repo root produces both or exits non-zero with a clear message. Task 3's workflow calls exactly `./build-installer.ps1`.

- [ ] **Step 1: Add dist/ to .gitignore**

Append to `.gitignore`:

```
dist/
```

- [ ] **Step 2: Write installer/chalk.iss**

```ini
; Chalk installer. Compiled by build-installer.ps1, which passes:
;   /DAppVersion=<version from csproj>  /DPublishDir=<absolute publish dir>
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\src\ScreenAnnotationTool\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{8B1E6F2A-4C7D-4E4B-9A31-D0C4E5A7C210}
AppName=Chalk
AppVersion={#AppVersion}
AppPublisher=James Diekman
AppPublisherURL=https://github.com/jdiekman/chalk-annotate
AppSupportURL=https://github.com/jdiekman/chalk-annotate/issues
DefaultDirName={localappdata}\Programs\Chalk
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=ChalkSetup-{#AppVersion}
SetupIconFile=..\src\ScreenAnnotationTool\chalk.ico
UninstallDisplayIcon={app}\Chalk.exe
; License shown as an info page (no forced "I accept" step) per spec.
InfoBeforeFile=..\LICENSE
; Matches the mutex created in App.OnStartup — prompts "close Chalk" before upgrade.
AppMutex=Chalk_SingleInstance_9f1c
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start Chalk when &Windows starts"

[Files]
Source: "{#PublishDir}\Chalk.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Chalk"; Filename: "{app}\Chalk.exe"
Name: "{autodesktop}\Chalk"; Filename: "{app}\Chalk.exe"; Tasks: desktopicon

[Registry]
; ValueData is deliberately UNQUOTED: StartupService.IsEnabled() string-compares it
; against Environment.ProcessPath, and a quoted value would never match.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Chalk"; ValueData: "{app}\Chalk.exe"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Chalk.exe"; Description: "Launch Chalk"; Flags: postinstall nowait skipifsilent
```

- [ ] **Step 3: Write build-installer.ps1**

```powershell
#Requires -Version 7
# Builds the Chalk installer + portable exe into dist/.
# Used identically on a dev machine and in CI (.github/workflows/release.yml).
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$csprojPath = Join-Path $repoRoot 'src/ScreenAnnotationTool/ScreenAnnotationTool.csproj'

$version = ([xml](Get-Content $csprojPath)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> element found in $csprojPath" }

dotnet publish $csprojPath -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$publishDir = Join-Path $repoRoot 'src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish'

$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue)?.Source
if (-not $iscc) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw 'Inno Setup 6 (ISCC.exe) not found. Install it with: winget install JRSoftware.InnoSetup'
}

& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $repoRoot 'installer/chalk.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }

Copy-Item (Join-Path $publishDir 'Chalk.exe') (Join-Path $repoRoot "dist/Chalk-$version-portable.exe") -Force
Write-Host "Built dist/ChalkSetup-$version.exe and dist/Chalk-$version-portable.exe"
```

Note: Inno Setup creates `OutputDir` (`dist/`) itself; the `Copy-Item` runs after ISCC, so `dist/` exists by then.

- [ ] **Step 4: Ensure Inno Setup 6 is installed locally**

Run: `winget list JRSoftware.InnoSetup`
If not installed: `winget install JRSoftware.InnoSetup --accept-source-agreements --accept-package-agreements`

- [ ] **Step 5: Run the build script and verify both artifacts**

Run:
```powershell
./build-installer.ps1
Get-ChildItem dist
```
Expected: script prints the success line; `dist/` contains `ChalkSetup-1.0.0.exe` (roughly 60–80 MB — it wraps a self-contained .NET exe) and `Chalk-1.0.0-portable.exe`.

- [ ] **Step 6: Commit**

```bash
git add .gitignore installer/chalk.iss build-installer.ps1
git commit -m "feat: Inno Setup installer script and local/CI build script"
```

---

### Task 3: Release workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: `./build-installer.ps1` from Task 2 (produces `dist/ChalkSetup-*.exe`, `dist/Chalk-*-portable.exe`); `<Version>` from Task 1.
- Produces: on pushing a tag `v<version>`, a GitHub Release with both artifacts attached and auto-generated notes.

- [ ] **Step 1: Write .github/workflows/release.yml**

```yaml
name: Release

on:
  push:
    tags: ['v*']

permissions:
  contents: write

jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Test
        run: dotnet test

      - name: Verify tag matches csproj version
        shell: pwsh
        run: |
          $version = ([xml](Get-Content src/ScreenAnnotationTool/ScreenAnnotationTool.csproj)).Project.PropertyGroup.Version |
              Where-Object { $_ } | Select-Object -First 1
          $tag = '${{ github.ref_name }}'.TrimStart('v')
          if ($tag -ne $version) {
              throw "Tag version '$tag' does not match csproj <Version> '$version'. Bump the csproj or retag."
          }

      - name: Build installer
        shell: pwsh
        run: ./build-installer.ps1

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: |
            dist/ChalkSetup-*.exe
            dist/Chalk-*-portable.exe
          generate_release_notes: true
```

(`windows-latest` has the .NET SDK and Inno Setup 6 preinstalled at `C:\Program Files (x86)\Inno Setup 6\`, which the build script's fallback lookup covers; `setup-dotnet` pins .NET 8 regardless of runner-image drift.)

- [ ] **Step 2: Local YAML sanity check**

No YAML linter is assumed on the machine; real validation happens when GitHub registers the workflow (Step 4). Locally, confirm the file has no tabs (YAML forbids them):

```powershell
if (Select-String -Path .github/workflows/release.yml -Pattern "`t") { throw 'Tabs found in YAML' } else { 'OK' }
```
Expected: `OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: release workflow — test, build installer, publish GitHub Release on v* tags"
```

- [ ] **Step 4: Verify GitHub registers the workflow**

After the commit is pushed to `main` (with the user's go-ahead):

Run: `gh workflow list`
Expected: a workflow named `Release` appears. (It only *runs* when a `v*` tag is pushed — done in Task 5.)

---

### Task 4: README update

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: Release artifact names from Task 2 (`ChalkSetup-<version>.exe`, `Chalk-<version>-portable.exe`).
- Produces: user-facing install docs; no downstream task depends on this.

- [ ] **Step 1: Replace the "no installer" feature bullet**

In `README.md`, replace:

```markdown
- Single self-contained `.exe`, no installer, no runtime to install on the target machine.
```

with:

```markdown
- **Easy install** — a small setup wizard, or a single portable `.exe` if you prefer;
  either way, no .NET runtime to install on the target machine.
```

- [ ] **Step 2: Add an Install section**

Insert immediately after the intro paragraph (before `## Features`):

```markdown
## Install

Grab **ChalkSetup-\<version\>.exe** from the
[latest release](https://github.com/jdiekman/chalk-annotate/releases/latest) and run it —
no admin rights needed. The installer offers a desktop shortcut, a
"start Chalk when Windows starts" option, and launches Chalk when it finishes
(look for the chalk icon in your system tray).

Prefer no installer? **Chalk-\<version\>-portable.exe** on the same page is a single
standalone exe — copy it anywhere and run it.

> **Windows SmartScreen:** Chalk isn't code-signed (yet), so the first run may show
> "Windows protected your PC". Click **More info → Run anyway**. This warning fades
> as the download builds reputation with Microsoft.
```

- [ ] **Step 3: Point the standalone-build section at the release artifacts**

In the `## Build a standalone .exe` section, replace the final paragraph:

```markdown
The single-file `Chalk.exe` lands in
`src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish/`. Copy it anywhere and
run it — no .NET install required on the target machine. Tick the **Startup** checkbox (or drop
a shortcut in your Startup folder) to have it ready whenever you record.
```

with:

```markdown
The single-file `Chalk.exe` lands in
`src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish/`. Copy it anywhere and
run it — no .NET install required on the target machine. Tick the **Startup** checkbox (or drop
a shortcut in your Startup folder) to have it ready whenever you record.

To build the installer too, install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup`) and run `./build-installer.ps1` — artifacts land in
`dist/`. Releases are built automatically by GitHub Actions when a `v*` tag is pushed.
```

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: installer-first install instructions with SmartScreen note"
```

---

### Task 5: Manual install verification (spec checklist)

**Files:** none (manual verification of `dist/ChalkSetup-1.0.0.exe` from Task 2).

**Interfaces:**
- Consumes: `dist/ChalkSetup-1.0.0.exe`.
- Produces: confirmation the installer meets the spec's testing checklist; gates tagging the first release.

- [ ] **Step 1: Fresh install**

Run `dist/ChalkSetup-1.0.0.exe`. Leave "Start Chalk when Windows starts" checked, tick the desktop shortcut, finish with "Launch Chalk" checked.
Expected: no UAC prompt; Chalk tray icon appears; desktop shortcut exists.

- [ ] **Step 2: Verify registrations**

Run:
```powershell
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Chalk
Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' |
    Get-ItemProperty | Where-Object DisplayName -eq 'Chalk' |
    Select-Object DisplayName, DisplayVersion, InstallLocation
```
Expected: `Chalk` Run value = `C:\Users\<user>\AppData\Local\Programs\Chalk\Chalk.exe` (no quotes); one uninstall entry, DisplayVersion `1.0.0`. Also check "Add or remove programs" shows Chalk with the chalk icon.

- [ ] **Step 3: Toolbar startup checkbox agrees with the installer**

Show the Chalk toolbar (Ctrl+Alt+D).
Expected: the **Startup** checkbox is checked (proves the unquoted registry value matches `StartupService.IsEnabled()`).

- [ ] **Step 4: Upgrade-while-running prompt**

With Chalk still running, run `dist/ChalkSetup-1.0.0.exe` again.
Expected: setup says Chalk is running and must be closed. Exit Chalk from the tray; setup proceeds; still exactly one entry in "Add or remove programs".

- [ ] **Step 5: Uninstall**

Uninstall Chalk from "Add or remove programs". Then:
```powershell
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Chalk -ErrorAction SilentlyContinue
Test-Path "$env:LOCALAPPDATA\Programs\Chalk"
Test-Path "$env:APPDATA\Chalk"
```
Expected: Run value gone (no output); install dir gone (`False`); settings dir still present (`True`) if Chalk ever saved settings.

- [ ] **Step 6: Portable exe still standalone**

Run `dist/Chalk-1.0.0-portable.exe` from `dist/`.
Expected: tray icon appears; drawing works; exit from tray.

- [ ] **Step 7: Report results**

No commit (no file changes). Report any failed step against the spec before tagging `v1.0.0`.
