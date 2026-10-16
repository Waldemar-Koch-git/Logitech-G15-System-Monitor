#define AppVersion "1.0.0"
#ifndef PayloadDir
  #define PayloadDir "..\bin\InstallerPayload"
#endif

[Setup]
AppId={{A9D834F0-6998-4788-AD00-851CB961A965}
AppName=G15 System Monitor
AppVersion={#AppVersion}
AppPublisher=G15 System Monitor Project
DefaultDirName={autopf}\G15 System Monitor
DefaultGroupName=G15 System Monitor
DisableDirPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=G15-System-Monitor-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\G15SystemMonitor.ico
UninstallDisplayIcon={app}\G15Dashboard.exe
CloseApplications=no
RestartApplications=no
SetupLogging=yes
LanguageDetectionMethod=none
UsePreviousLanguage=no
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"; InfoBeforeFile: "Information.txt"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"; InfoBeforeFile: "Hinweise.txt"
Name: "french"; MessagesFile: "compiler:Languages\French.isl,languages\french.isl"; InfoBeforeFile: "languages\french.txt"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl,languages\spanish.isl"; InfoBeforeFile: "languages\spanish.txt"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl,languages\italian.isl"; InfoBeforeFile: "languages\italian.txt"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl,languages\portuguese.isl"; InfoBeforeFile: "languages\portuguese.txt"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl,languages\dutch.isl"; InfoBeforeFile: "languages\dutch.txt"
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl,languages\danish.isl"; InfoBeforeFile: "languages\danish.txt"
Name: "swedish"; MessagesFile: "compiler:Languages\Swedish.isl,languages\swedish.isl"; InfoBeforeFile: "languages\swedish.txt"
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl,languages\norwegian.isl"; InfoBeforeFile: "languages\norwegian.txt"
Name: "finnish"; MessagesFile: "compiler:Languages\Finnish.isl,languages\finnish.isl"; InfoBeforeFile: "languages\finnish.txt"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl,languages\russian.isl"; InfoBeforeFile: "languages\russian.txt"

[CustomMessages]
english.DesktopIcon=Create a desktop shortcut
german.DesktopIcon=Desktop-Verknüpfung erstellen
english.Shortcuts=Shortcuts:
german.Shortcuts=Verknüpfungen:
english.Autostart=Start automatically when the installing Windows account signs in
german.Autostart=Bei Anmeldung des installierenden Windows-Kontos automatisch starten
english.StartBehavior=Startup:
german.StartBehavior=Startverhalten:
english.DesktopShortcutName=G15 System Monitor (installed)
german.DesktopShortcutName=G15 System Monitor (installiert)
english.LaunchNow=Start G15 System Monitor now
german.LaunchNow=G15 System Monitor jetzt starten
english.Prerequisites=Prerequisites
german.Prerequisites=Voraussetzungen
english.ExistingComponents=Components detected on this computer
german.ExistingComponents=Vorhandene Komponenten auf diesem Computer
english.LgsMissing=LGS was not found in the usual installation locations. The G15 display requires LGS with the LCD SDK; without it, only the preview is available.
german.LgsMissing=LGS wurde an den üblichen Installationsorten nicht gefunden. Für das G15-Display ist LGS mit LCD-SDK erforderlich; ohne diese Software steht nur die Vorschau zur Verfügung.
english.LgsFound=Logitech Gaming Software was found. The LCD also requires its matching x64 LogitechLcd.dll; the app searches for it in the LGS installation folder.
german.LgsFound=Logitech Gaming Software wurde gefunden. Das LCD benötigt zusätzlich deren passende x64-LogitechLcd.dll; die App sucht sie im LGS-Installationsordner.
english.PawnMissing=PawnIO was not found. Some temperatures may be unavailable. The driver will not be installed automatically.
german.PawnMissing=PawnIO wurde nicht gefunden. Einige Temperaturen können fehlen. Der Treiber wird nicht automatisch installiert.
english.PawnFound=A PawnIO installation entry was found. Use the app's diagnostics to check which sensors are actually available.
german.PawnFound=Ein PawnIO-Installationseintrag wurde gefunden. Die tatsächliche Sensorverfügbarkeit zeigt die Diagnose der App.
english.RuntimeNotice=.NET is included in this setup. Please close other versions of this dashboard using their tray menus before starting the app.
german.RuntimeNotice=.NET ist im Setup enthalten. Bitte andere Versionen dieses Dashboards vor dem Start über ihr Tray-Menü beenden.
english.AccountNotice=Autostart and task-based shortcuts apply to the Windows account running this setup with administrator privileges. If you enter another administrator's credentials, they apply to that other account.
german.AccountNotice=Autostart und Aufgaben-Verknüpfungen gelten für das Windows-Konto, unter dem dieses Setup mit Administratorrechten läuft. Bei Eingabe anderer Administrator-Zugangsdaten ist dies das andere Konto.
english.PrepareFailed=The existing installation could not be prepared safely. Please close the app and check Task Scheduler. Tasks belonging to other applications will not be overwritten.
german.PrepareFailed=Die vorhandene Installation konnte nicht sicher vorbereitet werden. Bitte die App beenden und die Aufgabenplanung prüfen. Fremde Aufgaben werden nicht überschrieben.
english.TaskFailed=The startup task could not be created. The files are installed, but shortcuts and autostart are not ready to use. Run setup again.
german.TaskFailed=Die Startaufgabe konnte nicht eingerichtet werden. Die Dateien sind installiert, aber Verknüpfung und Autostart sind nicht einsatzbereit. Setup erneut ausführen.
english.RemoveFailed=The app or its startup task could not be removed safely. Please close the app and try again.
german.RemoveFailed=Die App oder ihre Startaufgabe konnte nicht sicher entfernt werden. Bitte die App beenden und erneut versuchen.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Shortcuts}"
Name: "autostart"; Description: "{cm:Autostart}"; GroupDescription: "{cm:StartBehavior}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.png,LogitechLcd.dll"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Integration.ps1"; DestDir: "{app}\Setup"; Flags: ignoreversion
Source: "Integration.ps1"; Flags: dontcopy
Source: "Hinweise.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "Information.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "languages\*.txt"; DestDir: "{app}\Setup\Languages"; Flags: ignoreversion
Source: "licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Replace only the setup-owned shortcut in the other language when creating a new one.
Type: files; Name: "{commondesktop}\G15 System Monitor (installed).lnk"; Tasks: desktopicon; Languages: not (english)
Type: files; Name: "{commondesktop}\G15 System Monitor (installiert).lnk"; Tasks: desktopicon; Languages: not (german)
Type: files; Name: "{commondesktop}\G15 System Monitor (installé).lnk"; Tasks: desktopicon; Languages: not (french)
Type: files; Name: "{commondesktop}\G15 System Monitor (instalado).lnk"; Tasks: desktopicon; Languages: not (spanish or portuguese)
Type: files; Name: "{commondesktop}\G15 System Monitor (installato).lnk"; Tasks: desktopicon; Languages: not (italian)
Type: files; Name: "{commondesktop}\G15 System Monitor (geïnstalleerd).lnk"; Tasks: desktopicon; Languages: not (dutch)
Type: files; Name: "{commondesktop}\G15 System Monitor (installeret).lnk"; Tasks: desktopicon; Languages: not (danish)
Type: files; Name: "{commondesktop}\G15 System Monitor (installerat).lnk"; Tasks: desktopicon; Languages: not (swedish)
Type: files; Name: "{commondesktop}\G15 System Monitor (installert).lnk"; Tasks: desktopicon; Languages: not (norwegian)
Type: files; Name: "{commondesktop}\G15 System Monitor (asennettu).lnk"; Tasks: desktopicon; Languages: not (finnish)
Type: files; Name: "{commondesktop}\G15 System Monitor (установлено).lnk"; Tasks: desktopicon; Languages: not (russian)

[Icons]
Name: "{group}\G15 System Monitor"; Filename: "{sys}\schtasks.exe"; Parameters: "/Run /TN ""G15SystemMonitor-Installed"""; IconFilename: "{app}\G15Dashboard.exe"; WorkingDir: "{app}"; Flags: runminimized
Name: "{commondesktop}\{cm:DesktopShortcutName}"; Filename: "{sys}\schtasks.exe"; Parameters: "/Run /TN ""G15SystemMonitor-Installed"""; IconFilename: "{app}\G15Dashboard.exe"; WorkingDir: "{app}"; Tasks: desktopicon; Flags: runminimized

[Run]
Filename: "{sys}\schtasks.exe"; Parameters: "/Run /TN ""G15SystemMonitor-Installed"""; Description: "{cm:LaunchNow}"; Flags: postinstall nowait skipifsilent runhidden

[Code]
var
  Prerequisites: TOutputMsgWizardPage;

function Integration(const ScriptPath, Mode, Autostart: String): Boolean;
var
  ResultCode: Integer;
  Params: String;
begin
  Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptPath +
    '" -Mode ' + Mode + ' -AppDirectory "' + ExpandConstant('{app}') + '" -Autostart ' + Autostart +
    ' -LogPath "' + ExpandConstant('{tmp}\G15-integration-error.txt') + '"';
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params,
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure InitializeWizard;
var
  Lgs, Pawn: String;
begin
  Lgs := CustomMessage('LgsMissing');
  if FileExists(ExpandConstant('{pf64}\Logitech Gaming Software\LCore.exe')) or
     FileExists(ExpandConstant('{pf32}\Logitech Gaming Software\LCore.exe')) or
     FileExists(ExpandConstant('{pf64}\Logitech\Logitech Gaming Software\LCore.exe')) then
    Lgs := CustomMessage('LgsFound');
  Pawn := CustomMessage('PawnMissing');
  if RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO') then
    Pawn := CustomMessage('PawnFound');
  Prerequisites := CreateOutputMsgPage(wpWelcome, CustomMessage('Prerequisites'),
    CustomMessage('ExistingComponents'), Lgs + #13#10#13#10 + Pawn + #13#10#13#10 +
    CustomMessage('RuntimeNotice') + #13#10#13#10 + CustomMessage('AccountNotice'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  ExtractTemporaryFile('Integration.ps1');
  Result := '';
  if not Integration(ExpandConstant('{tmp}\Integration.ps1'), 'Stop', 'no') then
    Result := CustomMessage('PrepareFailed');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Auto: String;
begin
  if CurStep = ssPostInstall then begin
    Auto := 'no';
    if WizardIsTaskSelected('autostart') then Auto := 'yes';
    if not Integration(ExpandConstant('{app}\Setup\Integration.ps1'), 'Install', Auto) then
      RaiseException(CustomMessage('TaskFailed'));
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := Integration(ExpandConstant('{app}\Setup\Integration.ps1'), 'Remove', 'no');
  if not Result then MsgBox(CustomMessage('RemoveFailed'), mbError, MB_OK);
end;
