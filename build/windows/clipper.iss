; Установщик Clipper для Windows (Inno Setup 6).
; Собирается в GitHub Actions (.github/workflows/release.yml) или вручную: build\windows\build.ps1
; Ставится в профиль пользователя без прав администратора.

#define AppVersion GetEnv("CLIPPER_VERSION")
#if AppVersion == ""
  #define AppVersion "0.0.0-dev"
#endif

[Setup]
AppId={{6F4C2B1E-3C8A-4E8B-9D57-2A1F0C9E7B11}
AppName=Clipper
AppVersion={#AppVersion}
AppPublisher=Clipper
DefaultDirName={localappdata}\Programs\Clipper
DefaultGroupName=Clipper
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=ClipperSetup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Clipper.exe

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Результат dotnet publish (см. build.ps1 / release.yml).
Source: "..\..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Clipper"; Filename: "{app}\Clipper.exe"
Name: "{autodesktop}\Clipper"; Filename: "{app}\Clipper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Clipper.exe"; Description: "{cm:LaunchProgram,Clipper}"; Flags: nowait postinstall skipifsilent
