; Script Inno Setup pour Sono-Sonnette.
; Compilation : ouvrir ce fichier avec Inno Setup (https://jrsoftware.org/isinfo.php)
; ou en ligne de commande : iscc installer\SonoSonnette.iss
;
; Prérequis : avoir publié l'application au préalable avec :
;   dotnet publish src\SonoSonnette.App\SonoSonnette.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish\win-x64

#define MyAppName "Sono-Sonnette"
#define MyAppVersion "1.0.0"
#define MyAppExeName "SonoSonnette.exe"
#define MyPublishDir "..\publish\win-x64"

[Setup]
AppId={{6C6C7D2E-6C4A-4C7E-9B7A-2C1E7B1E6E10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=SonoSonnette-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
; Installation par utilisateur courant : pas besoin des droits administrateur,
; cohérent avec le démarrage automatique via la clé de registre HKCU\...\Run.
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le Bureau"; GroupDescription: "Raccourcis supplémentaires :"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; L'application gère elle-même cette valeur (démarrage automatique) à l'exécution ;
; on ne fait ici que garantir sa suppression propre lors de la désinstallation.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "SonoSonnette"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent
