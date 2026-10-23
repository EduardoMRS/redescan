; Instalador do RedeScan Full (Windows) via Inno Setup 6.1+.
; Gera um setup.exe com atalhos (Menu Iniciar e Área de Trabalho) e desinstalador.
; Durante a instalação, baixa o que o app precisa e ainda não existe na máquina:
;   - Microsoft Edge WebView2 Runtime (navegador embutido) — se ausente;
;   - ffmpeg (vídeo das câmeras RTSP), verificado por SHA-256.
; Se um download falhar, a instalação continua: o RedeScan oferece baixar o
; ffmpeg depois, de dentro do app.
;
; A pasta publicada e a versão chegam por /D na linha de comando (ISCC):
;   iscc /DPublishDir="full\win-x64" /DAppVersion="1.0.0" scripts\packaging\redescan.iss

#ifndef PublishDir
  #define PublishDir "full\win-x64"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define WebView2Url "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
#define FfmpegUrl "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"

[Setup]
AppId={{7B2E9C4A-3D1F-4A6B-9E2C-6F0A1B2C3D4E}
AppName=RedeScan
AppVersion={#AppVersion}
AppPublisher=RedeScan
DefaultDirName={autopf}\RedeScan
DefaultGroupName=RedeScan
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=RedeScan-Full-Windows-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
SetupIconFile=..\..\src\RedeScan.UI\Assets\redescan.ico
UninstallDisplayIcon={app}\RedeScan.exe
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"
Name: "ffmpeg"; Description: "Baixar o ffmpeg (necessário para ver as câmeras RTSP)"; GroupDescription: "Componentes extras:"

[Files]
; Inclui toda a pasta publicada, recursivamente.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
; Logs do app ficam na pasta de instalação, graváveis pelos usuários.
Name: "{app}\logs"; Permissions: users-modify

[Icons]
Name: "{group}\RedeScan"; Filename: "{app}\RedeScan.exe"
Name: "{group}\Pasta de logs do RedeScan"; Filename: "{app}\logs"
Name: "{group}\Desinstalar RedeScan"; Filename: "{uninstallexe}"
Name: "{autodesktop}\RedeScan"; Filename: "{app}\RedeScan.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RedeScan.exe"; Description: "Abrir o RedeScan"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\ffmpeg"

[Code]
var
  DownloadPage: TDownloadWizardPage;

function WebView2Installed(): Boolean;
var
  Version: String;
begin
  // Chaves documentadas pela Microsoft para detectar o WebView2 Runtime.
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), 'Baixando componentes necessários…', @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  HasDownloads: Boolean;
begin
  Result := True;
  if CurPageID <> wpReady then
    exit;

  HasDownloads := False;
  DownloadPage.Clear;
  if not WebView2Installed() then
  begin
    DownloadPage.Add('{#WebView2Url}', 'MicrosoftEdgeWebview2Setup.exe', '');
    HasDownloads := True;
  end;
  if WizardIsTaskSelected('ffmpeg') then
  begin
    DownloadPage.Add('{#FfmpegUrl}', 'ffmpeg.zip', '');
    DownloadPage.Add('{#FfmpegUrl}.sha256', 'ffmpeg.zip.sha256', '');
    HasDownloads := True;
  end;

  if not HasDownloads then
    exit;

  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      // Sem internet/servidor fora: segue a instalação; o app oferece baixar depois.
      Log('Download de componentes falhou: ' + GetExceptionMessage);
      MsgBox('Não foi possível baixar todos os componentes extras agora.' + #13#10 +
             'A instalação vai continuar; o RedeScan oferece baixar o ffmpeg de dentro do app.',
             mbInformation, MB_OK);
    end;
  finally
    DownloadPage.Hide;
  end;
end;

procedure InstallWebView2;
var
  ResultCode: Integer;
  Setup: String;
begin
  Setup := ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe');
  if not FileExists(Setup) then
    exit;
  WizardForm.StatusLabel.Caption := 'Instalando o Microsoft Edge WebView2 Runtime…';
  if not Exec(Setup, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Falha ao executar o instalador do WebView2.')
  else
    Log('Instalador do WebView2 terminou com código ' + IntToStr(ResultCode));
end;

procedure InstallFfmpeg;
var
  ResultCode: Integer;
  Zip, Sha, Dest, Script: String;
begin
  Zip := ExpandConstant('{tmp}\ffmpeg.zip');
  Sha := ExpandConstant('{tmp}\ffmpeg.zip.sha256');
  Dest := ExpandConstant('{app}\ffmpeg');
  if not FileExists(Zip) then
    exit;
  WizardForm.StatusLabel.Caption := 'Verificando e extraindo o ffmpeg…';

  // PowerShell: confere o SHA-256 publicado, extrai e copia só ffmpeg/ffprobe.
  Script :=
    '$ErrorActionPreference=''Stop'';' +
    '$exp=((Get-Content -LiteralPath ''' + Sha + ''' -Raw).Trim() -split ''\s+'')[0];' +
    '$act=(Get-FileHash -LiteralPath ''' + Zip + ''' -Algorithm SHA256).Hash;' +
    'if ($act -ne $exp.ToUpper()) { throw ''SHA-256 do ffmpeg nao confere'' };' +
    '$x=Join-Path $env:TEMP (''rs-ffmpeg-'' + [guid]::NewGuid());' +
    'Expand-Archive -LiteralPath ''' + Zip + ''' -DestinationPath $x -Force;' +
    'New-Item -ItemType Directory -Force -Path ''' + Dest + ''' | Out-Null;' +
    'Get-ChildItem -Path $x -Recurse -Include ffmpeg.exe,ffprobe.exe | Copy-Item -Destination ''' + Dest + ''' -Force;' +
    'Remove-Item -Recurse -Force $x';

  if not Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "' + Script + '"', '',
              SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    Log('Instalação do ffmpeg falhou (código ' + IntToStr(ResultCode) + '). O app oferecerá baixar depois.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    InstallWebView2;
    InstallFfmpeg;
  end;
end;
