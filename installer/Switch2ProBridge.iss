; Inno Setup script. Build with build-installer.ps1, which publishes the app and fetches ViGEmBus first.

#define AppName "Switch 2 Pro Bridge"
#define AppExe "Switch2ProBridge.exe"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define ViGEmBusSetup "ViGEmBus_1.22.0_x64_x86_arm64.exe"

[Setup]
AppId={{8E3B6C2A-5F41-4D8B-9C77-2A1E4F0B9D13}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Caio Mendes
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; ViGEmBus is a kernel driver, so installing it needs admin rights.
PrivilegesRequired=admin
; The Game Bar tweak writes HKCU; with a normal UAC prompt that's still the signed-in user's hive.
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Switch2ProBridge-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "gamebarfix"; Description: "Don't open Xbox Game Bar when the Home button is pressed (avoids the 'ms-gamebar' pop-up)"; GroupDescription: "Windows settings:"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "redist\ViGEmBus-LICENSE.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "redist\{#ViGEmBusSetup}"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same as Settings > Gaming > Game Bar > "Open Xbox Game Bar using this button on a controller" = off.
Root: HKCU; Subkey: "Software\Microsoft\GameBar"; ValueType: dword; ValueName: "UseNexusForGameBarEnabled"; ValueData: 0; Tasks: gamebarfix

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  RestartNeeded: Boolean;

function ViGEmBusInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\ViGEmBus');
end;

procedure InstallViGEmBus;
var
  ResultCode: Integer;
begin
  if ViGEmBusInstalled then
    exit;

  WizardForm.StatusLabel.Caption := 'Installing ViGEmBus driver (virtual Xbox controller)...';
  ExtractTemporaryFile('{#ViGEmBusSetup}');
  // Silent switches for ViGEmBus's Advanced Installer setup.
  if not Exec(ExpandConstant('{tmp}\{#ViGEmBusSetup}'), '/exenoui /qn /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;

  if ResultCode = 3010 then
    RestartNeeded := True
  else if ResultCode <> 0 then
    SuppressibleMsgBox('The ViGEmBus driver could not be installed (code ' + IntToStr(ResultCode) + ').' + #13#10 +
      'The app will run, but games will not see an Xbox controller until ViGEmBus is installed.' + #13#10 +
      'You can get it from https://github.com/nefarius/ViGEmBus/releases',
      mbError, MB_OK, IDOK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallViGEmBus;
end;

function NeedRestart: Boolean;
begin
  Result := RestartNeeded;
end;
