; Elifoot 98 Registrador - instalador separado
; Contem apenas o CRACK.EXE + DOSBox + UI Registrador (frontend).
; Distribuido separadamente do launcher principal por questao de licenciamento
; (CRACK.EXE eh o keygen original do Elifoot 98 de Andre Elias).
;
; Compilar no Windows: iscc /DAppVersion=0.3.0 setup-registrador.iss

#define AppName        "Elifoot 98 Registrador"
#ifndef AppVersion
  #define AppVersion   "0.0.0-dev"
#endif
#define AppPublisher   "elifoot98-launcher contributors"
#define AppURL         "https://github.com/juan-silveira/elifoot98-launcher"
#define AppExeName     "ElifootRegistrador.exe"

[Setup]
AppId={{2C8D4A97-53F1-4E28-BA76-9E5C3F1D8B0A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName=C:\Elifoot98Registrador
DefaultGroupName=Elifoot 98 Registrador
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=elifoot98-registrador-setup-{#AppVersion}
SetupIconFile=..\src\elifoot.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; UI Registrador (Elifoot Registrador)
Source: "..\src-registrador\bin\Release\net48\ElifootRegistrador.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src-registrador\bin\Release\net48\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; CRACK.EXE original do Elifoot 98 (Andre Elias, 1998)
Source: "..\game\CRACK.EXE"; DestDir: "{app}"; Flags: ignoreversion

; DOSBox-Staging (necessario pra rodar CRACK.EXE que eh DOS 16-bit)
Source: "..\vendor\dosbox\*"; DestDir: "{app}\vendor\dosbox"; Flags: ignoreversion recursesubdirs createallsubdirs

; VC++ Runtime (por seguranca, se Registrador precisar)
Source: "..\vendor\vcredist\vc_redist.x86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Documentacao
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Elifoot 98 Registrador"; Filename: "{app}\{#AppExeName}"
Name: "{autoprograms}\Desinstalar Elifoot 98 Registrador"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Elifoot 98 Registrador"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\vc_redist.x86.exe"; Parameters: "/install /quiet /norestart"; \
  StatusMsg: "Instalando Microsoft Visual C++ Runtime..."; \
  Check: NeedsVcRedistX86

Filename: "{app}\{#AppExeName}"; Description: "Abrir Registrador"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsVcRedistX86: Boolean;
var
  Installed: Cardinal;
begin
  if RegQueryDWordValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86', 'Installed', Installed) or
     RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86', 'Installed', Installed) then
    Result := (Installed <> 1)
  else
    Result := True;
end;

function InitializeSetup(): Boolean;
var
  NetFxVer: Cardinal;
  ErrCode: Integer;
begin
  Result := True;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', NetFxVer) then
  begin
    if NetFxVer < 528040 then
    begin
      if MsgBox('Este Registrador requer .NET Framework 4.8 (ou superior). Instalar agora?' + #13#10 +
                'Voce sera redirecionado ao download oficial da Microsoft.',
                mbConfirmation, MB_YESNO) = IDYES then
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOW, ewNoWait, ErrCode);
      Result := False;
    end;
  end;
end;
