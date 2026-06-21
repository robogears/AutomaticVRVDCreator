; AutoVRVD installer (Inno Setup 6). Per-machine install to Program Files.
; Build:  ISCC.exe /DAppVersion=0.1.4 installer\AutoVRVD.iss
; CI passes the version from the tag; AppVersion defaults to 0.0.0 for local test builds.
; Expects the published single-file exe at ..\publish\AutoVRVD.exe.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{B7E5C0A1-9D3F-4A2E-8C6B-1A2B3C4D5E6F}
AppName=AutoVRVD
AppVersion={#AppVersion}
AppVerName=AutoVRVD {#AppVersion}
AppPublisher=robogears
AppPublisherURL=https://github.com/robogears/AutomaticVRVDCreator
AppSupportURL=https://github.com/robogears/AutomaticVRVDCreator/issues
AppUpdatesURL=https://github.com/robogears/AutomaticVRVDCreator/releases
DefaultDirName={autopf}\AutoVRVD
DefaultGroupName=AutoVRVD
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\installer-out
OutputBaseFilename=AutoVRVD-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\AutoVRVD\AutoVRVD.ico
UninstallDisplayIcon={app}\AutoVRVD.exe
UninstallDisplayName=AutoVRVD
VersionInfoVersion={#AppVersion}
VersionInfoCompany=robogears
VersionInfoProductName=AutoVRVD

; Cleanly close a running AutoVRVD before replacing its files (used on install + silent update).
CloseApplications=yes
CloseApplicationsFilter=AutoVRVD.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Start AutoVRVD automatically when I sign in"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\AutoVRVD.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\AutoVRVD"; Filename: "{app}\AutoVRVD.exe"
Name: "{autodesktop}\AutoVRVD"; Filename: "{app}\AutoVRVD.exe"; Tasks: desktopicon

[Run]
; Fresh interactive install only: enable per-user autostart AS THE SIGNED-IN USER (writes HKCU, not the
; elevated admin hive). Skipped on silent auto-updates so it doesn't override the user's later choice.
Filename: "{app}\AutoVRVD.exe"; Parameters: "--set-autostart"; Flags: runasoriginaluser runhidden waituntilterminated; Tasks: autostart; Check: not WizardSilent
; Interactive install: optional "Launch AutoVRVD" checkbox on the Finished page (runs de-elevated).
Filename: "{app}\AutoVRVD.exe"; Description: "Launch AutoVRVD"; Flags: runasoriginaluser nowait postinstall skipifsilent
; Silent (auto-update) install: relaunch the tray app as the signed-in user.
Filename: "{app}\AutoVRVD.exe"; Flags: runasoriginaluser nowait; Check: WizardSilent

; Note: the autostart entry is a per-user HKCU "Run" value (written by --set-autostart as the signed-in
; user). The elevated uninstaller can't reach that user's hive, so we don't remove it here; a stale Run
; value pointing at the removed exe is harmless (Windows skips missing autostart targets). The user can
; also toggle it any time in the app's Settings ("Start with Windows").
