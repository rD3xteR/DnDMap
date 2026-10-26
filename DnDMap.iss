; Inno Setup script for DnDMap
; Invoked by build-installer.ps1 (locally) and .github/workflows/release.yml (CI).
;
; Defines (can be overridden from the command line with /D):
;   MyAppVersion - version shown in the installer / Add-Remove Programs
;   PublishDir   - directory with the published application (relative to this script)

#define MyAppName "DnDMap"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "publish"
#endif

[Setup]
AppId={{A62915CB-4028-404E-A968-F2A4342C8AC7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=rD3xteR
AppPublisherURL=https://github.com/rD3xteR/DnDMap
AppSupportURL=https://github.com/rD3xteR/DnDMap/issues
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user installation: no admin rights / UAC prompt required
PrivilegesRequired=lowest
MinVersion=10.0
ArchitecturesAllowed=x64compatible
OutputDir=Setup
OutputBaseFilename=DnDMap-Setup
SetupIconFile=DnDMap\src\logo.ico
UninstallDisplayIcon={app}\DnDMap.exe
UninstallDisplayName=DnDMap
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DnDMap"; Filename: "{app}\DnDMap.exe"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DnDMap"; Filename: "{app}\DnDMap.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DnDMap.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
