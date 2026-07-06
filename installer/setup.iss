; Elifoot 98 Launcher - Inno Setup script
; Requer Inno Setup 6+ (https://jrsoftware.org/isdl.php)
; Compilar no Windows: iscc setup.iss
;
; Pre-requisitos antes de compilar:
;   1. Build do launcher:  dotnet build -c Release  (em ../src)
;   2. Fetch das deps:     bash ../scripts/fetch-deps.sh
;   3. Colocar arquivos do Elifoot 98 em ../game/

#define AppName        "Elifoot 98 Launcher"
#define AppVersion     "0.1.0"
#define AppPublisher   "elifoot98-launcher contributors"
#define AppURL         "https://github.com/juan-silveira/elifoot98-launcher"
#define AppExeName     "ElifootLauncher.exe"

[Setup]
AppId={{7A5F9C48-E8B3-4A6D-9F2E-1B4D6E8F0A21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName=C:\Elifoot98Launcher
DefaultGroupName=Elifoot 98
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=elifoot98-launcher-setup-{#AppVersion}
SetupIconFile=..\src\elifoot.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Precisa admin pra criar pasta na raiz do C:. Depois de criada, herda ACL default que permite ao usuario escrever (necessario porque otvdm.ini eh regravado a cada abertura).
PrivilegesRequired=admin
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Launcher e dependencias .NET
Source: "..\src\bin\Release\net48\ElifootLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\bin\Release\net48\*.dll";               DestDir: "{app}"; Flags: ignoreversion

; otvdm (16-bit emulator para ELIFOOT.EXE e EDITEQ.EXE)
Source: "..\vendor\otvdm\*"; DestDir: "{app}\vendor\otvdm"; Flags: ignoreversion recursesubdirs createallsubdirs

; DOSBox-Staging (para CRACK.EXE)
Source: "..\vendor\dosbox\*"; DestDir: "{app}\vendor\dosbox"; Flags: ignoreversion recursesubdirs createallsubdirs

; Arquivos do jogo (ficam em ..\game\)
Source: "..\game\*"; DestDir: "{app}\game"; Flags: ignoreversion recursesubdirs createallsubdirs

; Documentacao
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";   DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Elifoot 98";             Filename: "{app}\{#AppExeName}"
Name: "{autoprograms}\Desinstalar Elifoot 98"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Elifoot 98";              Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir Elifoot 98 Launcher"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  NetFxVer: Cardinal;
  ErrCode: Integer;
begin
  Result := True;
  // .NET Framework 4.8 tem Release >= 528040 (Win 10 1903+ e Win 11 ja vem pre-instalado)
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', NetFxVer) then
  begin
    if NetFxVer < 528040 then
    begin
      if MsgBox('Este launcher requer .NET Framework 4.8 (ou superior). Instalar agora?' + #13#10 +
                'Voce sera redirecionado ao download oficial da Microsoft.',
                mbConfirmation, MB_YESNO) = IDYES then
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOW, ewNoWait, ErrCode);
      Result := False;
    end;
  end;
end;
