; VirtualMirage installer (Inno Setup 6). Per-USER install to %LocalAppData%\Programs (no admin, no UAC).
; Build:  ISCC.exe /DAppVersion=0.1.8 installer\VirtualMirage.iss
; CI passes the version from the tag; AppVersion defaults to 0.0.0 for local test builds.
; Expects the published single-file exe at ..\publish\VirtualMirage.exe.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{2BD7950A-B9C3-46EC-BE57-2B3297A62E37}
AppName=VirtualMirage
AppVersion={#AppVersion}
AppVerName=VirtualMirage {#AppVersion}
AppPublisher=robogears
AppPublisherURL=https://github.com/robogears/VirtualMirage
AppSupportURL=https://github.com/robogears/VirtualMirage/issues
AppUpdatesURL=https://github.com/robogears/VirtualMirage/releases
DefaultDirName={autopf}\VirtualMirage
DefaultGroupName=VirtualMirage
DisableProgramGroupPage=yes
DisableDirPage=auto
; lowest => Setup never elevates, so there is NO UAC prompt on install or on silent auto-update.
; {autopf} then resolves to {userpf} = %LocalAppData%\Programs, a user-writable location.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\installer-out
OutputBaseFilename=VirtualMirage-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\VirtualMirage\VirtualMirage.ico
UninstallDisplayIcon={app}\VirtualMirage.exe
UninstallDisplayName=VirtualMirage
VersionInfoVersion={#AppVersion}
VersionInfoCompany=robogears
VersionInfoProductName=VirtualMirage

; Cleanly close a running VirtualMirage before replacing its files (used on install + silent update).
CloseApplications=yes
CloseApplicationsFilter=VirtualMirage.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Start VirtualMirage automatically when I sign in"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\VirtualMirage.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\VirtualMirage"; Filename: "{app}\VirtualMirage.exe"
Name: "{autodesktop}\VirtualMirage"; Filename: "{app}\VirtualMirage.exe"; Tasks: desktopicon

[Run]
; Setup runs as the signed-in user (no elevation), so HKCU writes and the relaunch land in the right
; place directly — no runasoriginaluser needed. Fresh interactive install only: enable per-user autostart.
Filename: "{app}\VirtualMirage.exe"; Parameters: "--set-autostart"; Flags: runhidden waituntilterminated; Tasks: autostart; Check: not WizardSilent
; Interactive install: optional "Launch VirtualMirage" checkbox on the Finished page.
Filename: "{app}\VirtualMirage.exe"; Description: "Launch VirtualMirage"; Flags: nowait postinstall skipifsilent
; Silent (auto-update) install: relaunch the tray app.
Filename: "{app}\VirtualMirage.exe"; Flags: nowait; Check: WizardSilent

[UninstallRun]
; Clean up the per-user autostart value on uninstall (uninstaller runs as the user, so HKCU is reachable).
Filename: "{app}\VirtualMirage.exe"; Parameters: "--unset-autostart"; Flags: runhidden; RunOnceId: "unsetautostart"
