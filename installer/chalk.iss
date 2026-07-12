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
UninstallDisplayName=Chalk
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
; Quote the executable so Windows never treats a space-containing path as multiple commands.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Chalk"; ValueData: """{app}\Chalk.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Chalk.exe"; Description: "Launch Chalk"; Flags: postinstall nowait skipifsilent
