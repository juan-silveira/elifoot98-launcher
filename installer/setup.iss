; Elifoot 98 Launcher - Inno Setup script
; Requer Inno Setup 6+ (https://jrsoftware.org/isdl.php)
; Compilar no Windows: iscc setup.iss
;
; Pre-requisitos antes de compilar:
;   1. Build do launcher:  dotnet build -c Release  (em ../src)
;   2. Fetch das deps:     bash ../scripts/fetch-deps.sh
;   3. Colocar arquivos do Elifoot 98 em ../game/

#define AppName        "Elifoot 98 Launcher"
; AppVersion pode ser sobrescrito por linha de comando: iscc /DAppVersion=0.1.5 setup.iss
#ifndef AppVersion
  #define AppVersion   "0.0.0-dev"
#endif
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
; Launcher e dependencias .NET (dlls opcionais — se nao houver, tudo bem)
Source: "..\src\bin\Release\net48\ElifootLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
; O .exe.config (gerado pelo SDK) tem <supportedRuntime sku=".NETFramework,Version=v4.8">: sem
; ele, num Windows sem o 4.8 o launcher abre no .NET antigo e pode fechar sem mensagem; com ele,
; o proprio Windows avisa que falta o .NET Framework 4.8. Por isso e obrigatorio.
Source: "..\src\bin\Release\net48\ElifootLauncher.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\bin\Release\net48\*.dll";               DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; otvdm (16-bit emulator para ELIFOOT.EXE e EDITEQ.EXE)
Source: "..\vendor\otvdm\*"; DestDir: "{app}\vendor\otvdm"; Flags: ignoreversion recursesubdirs createallsubdirs

; VC++ 2015-2022 Redistributable x86 (necessario pro otvdm em Tiny10 e similares)
Source: "..\vendor\vcredist\vc_redist.x86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Arquivos do jogo (ficam em ..\game\). O CRACK.EXE nao vai: a contra-senha
; do "Ativar todos os recursos" e calculada pelo proprio launcher.
Source: "..\game\*"; DestDir: "{app}\game"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "CRACK.EXE"
; Copia intocada (sem os saves) para a biblioteca de patches voltar aos times originais
Source: "..\game\*"; DestDir: "{app}\original"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "CRACK.EXE,JOGOS\*"

; Documentacao
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";   DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; Usuario comum precisa gravar na pasta (eli.cod/elif98.ini em vendor\otvdm\WINDOWS,
; otvdm.ini, saves em game\JOGOS). Sem isso, em Windows com ACL de C:\ restrita, o
; otvdm (32-bit sem manifest) grava em silencio na VirtualStore do usuario.
Name: "{app}"; Permissions: users-modify

[InstallDelete]
; Equipes que nao fazem parte do set original (removidas na v0.6.4) — apaga em upgrades
Type: files; Name: "{app}\game\EQUIPAS\ACADEMIC.EFT"
Type: files; Name: "{app}\game\EQUIPAS\ADMIRA.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AEK_LARN.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AJAX.EFT"
Type: files; Name: "{app}\game\EQUIPAS\ALCANENE.EFT"
Type: files; Name: "{app}\game\EQUIPAS\ALVERCA.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AL_AHLY.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AMAZULU.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AMERICA.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AMERI_SP.EFT"
Type: files; Name: "{app}\game\EQUIPAS\AMORA.EFT"
Type: files; Name: "{app}\game\EQUIPAS\ANADIA.EFT"
Type: files; Name: "{app}\game\EQUIPAS\ANDERLEC.EFT"
Type: files; Name: "{app}\game\EQUIPAS\MALMOE.EFT"
; CRACK.EXE e DOSBox instalados pela v0.6.5/v0.6.6, nao usados mais
Type: files; Name: "{app}\game\CRACK.EXE"
Type: filesandordirs; Name: "{app}\vendor\dosbox"

[Icons]
Name: "{autoprograms}\Elifoot 98";             Filename: "{app}\{#AppExeName}"
Name: "{autoprograms}\Desinstalar Elifoot 98"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Elifoot 98";              Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Instala VC++ Runtime silenciosamente (exit 1638 = ja instalado, tudo bem)
Filename: "{tmp}\vc_redist.x86.exe"; Parameters: "/install /quiet /norestart"; \
  StatusMsg: "Instalando Microsoft Visual C++ Runtime..."; \
  Check: NeedsVcRedistX86

Filename: "{app}\{#AppExeName}"; Description: "Abrir Elifoot 98 Launcher"; Flags: nowait postinstall skipifsilent
; Atualizacao pelo launcher (/SILENT): abre o launcher novo no fim, como o usuario (nao admin)
Filename: "{app}\{#AppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent

[Code]
function NeedsVcRedistX86: Boolean;
var
  Installed: Cardinal;
begin
  // Chave do VC++ 2015-2022 x86 Redistributable — se Installed=1, ja tem
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
  // .NET Framework 4.8 tem Release >= 528040 (Win 10 1903+ e Win 11 ja vem pre-instalado;
  // o Win 10 1809, minimo aceito, vem com 4.7.2). Sem a chave = sem .NET 4.x: tambem falta.
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', NetFxVer) then
    NetFxVer := 0;
  if NetFxVer < 528040 then
  begin
    if MsgBox('O Elifoot 98 Launcher precisa do Microsoft .NET Framework 4.8 (ou mais novo), ' +
              'que nao foi encontrado neste computador.' + #13#10#13#10 +
              'Sem ele o launcher nao abre. Deseja abrir a pagina oficial da Microsoft para baixa-lo agora?' + #13#10#13#10 +
              'Depois de instalar o .NET Framework 4.8 (pode pedir para reiniciar o Windows), ' +
              'execute este instalador de novo.',
              mbError, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOW, ewNoWait, ErrCode);
    Result := False;
  end;
end;
