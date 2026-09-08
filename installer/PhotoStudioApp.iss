; Inno Setup script for PhotoStudioApp.
; Produces a single, double-clickable setup.exe with a Start Menu shortcut,
; optional desktop icon, and a proper uninstaller - installable on Windows 7
; SP1 and above (32-bit or 64-bit).
;
; HOW TO BUILD THE INSTALLER (on a Windows machine):
;   1. Build the app first in Release mode (see ..\build\publish.ps1 or just
;      Build > Rebuild Solution in Visual Studio with Configuration=Release,
;      Platform=x86). This script packages whatever is in
;      ..\PhotoStudioApp\bin\Release - build that first or this has nothing
;      to package.
;   2. Install Inno Setup (free): https://jrsoftware.org/isinfo.php
;   3. Either:
;        a) Right-click this file -> "Compile" (if you installed the Inno
;           Setup IDE), or
;        b) From a command prompt: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" PhotoStudioApp.iss
;      Both produce installer\Output\PhotoStudioApp-Setup.exe
;
; You can also just run build\build-installer.ps1, which does both the app
; build and the installer compile in one go (requires msbuild AND Inno
; Setup's ISCC.exe to be on PATH, or installed in their default locations).

#define MyAppName "Photo Studio App"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Photo Studio"
#define MyAppExeName "PhotoStudioApp.exe"
#define MyAppSourceDir "..\PhotoStudioApp\bin\Release"

[Setup]
; A fixed AppId lets future versions upgrade in place instead of installing
; side-by-side. Keep this unchanged across releases.
AppId={{2F9A6A3E-6B0E-4A58-9A4A-7A2C1E0E9B10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; The app is built for the x86 platform (keeps the SQLite native interop
; simple across 32-bit and 64-bit Windows 7), so we deliberately do NOT set
; ArchitecturesInstallIn64BitMode - {autopf} then correctly resolves to
; "Program Files (x86)" on 64-bit Windows and "Program Files" on 32-bit.
OutputDir=Output
OutputBaseFilename=PhotoStudioApp-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Windows 7 SP1 = version 6.1, build 7601.
MinVersion=6.1.7601
PrivilegesRequired=admin
SetupIconFile=..\PhotoStudioApp\Properties\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
ArchitecturesAllowed=x86 x64 arm64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Pulls in PhotoStudioApp.exe, its .config, and the SQLite native interop
; subfolders (x86\SQLite.Interop.dll, x64\SQLite.Interop.dll) that NuGet
; placed next to it at build time.
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leave the studio's database and settings alone on uninstall (they live
; under %ProgramData%\PhotoStudioApp or %LocalAppData%\PhotoStudioApp, not
; under {app}) so re-installing or upgrading never loses studio history.
; If you ever want an "uninstall and wipe data" option, add a checkbox task
; here that conditionally deletes those folders - deliberately not done by
; default to avoid accidental data loss.
