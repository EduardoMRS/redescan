; Instalador do RedeScan Full (Windows) via Inno Setup.
; Gera um setup.exe com atalhos (Menu Iniciar e Área de Trabalho) e desinstalador.
; A pasta publicada e a versão chegam por /D na linha de comando (ISCC):
;   iscc /DPublishDir="full\win-x64" /DAppVersion="1.0.0" scripts\packaging\redescan.iss

#ifndef PublishDir
  #define PublishDir "full\win-x64"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

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

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
; Inclui toda a pasta publicada, recursivamente.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\RedeScan"; Filename: "{app}\RedeScan.exe"
Name: "{group}\Desinstalar RedeScan"; Filename: "{uninstallexe}"
Name: "{autodesktop}\RedeScan"; Filename: "{app}\RedeScan.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RedeScan.exe"; Description: "Abrir o RedeScan"; Flags: nowait postinstall skipifsilent
