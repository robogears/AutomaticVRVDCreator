# CLAUDE.md — AutoVRVD

Guidance for working in this repo. Read this first.

## What this is

**AutoVRVD** is a Windows **system-tray app** (C# / .NET 8 / WinForms) that auto-creates a
**4K (3840×2160) @ 120 Hz virtual display** when the user connects to the PC in VR through
**Virtual Desktop**, makes it the sole primary display, disables the physical monitors, and
**restores everything exactly** on disconnect. It lets the user play flat games at 4K120 on a
headless virtual screen viewed in VR.

It does for *Virtual Desktop* what Apollo/Sunshine do for a Moonlight stream, and it reuses the
**SUDOVDA** virtual-display driver (already installed on the dev machine via Apollo, at
`C:\Program Files\Apollo\drivers\sudovda\`). The architecture deliberately mirrors Apollo's
two-layer design + `libdisplaydevice`.

End-user docs live in [README.md](README.md). This file is for developers/agents.

## Build & run

**The .NET 8 SDK is installed *user-local* and is NOT on the system PATH** (the machine only has
.NET runtimes on PATH). Always build with the helper or the explicit SDK path:

```powershell
.\build.ps1                       # Release build (auto-locates the SDK)
.\build.ps1 -Publish              # framework-dependent publish -> .\publish
.\build.ps1 -Run                  # build + launch
# or directly:
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src\AutoVRVD\AutoVRVD.csproj -c Release
```

- Target: `net8.0-windows`, x64, `WinExe` (no console), `AllowUnsafeBlocks`, nullable + implicit usings on.
- **No external NuGet dependencies** — custom logger, `System.Text.Json`, and P/Invoke only. Keep it that way (hermetic build).
- Output: `src\AutoVRVD\bin\<Config>\net8.0-windows\AutoVRVD.exe`.

## Testing (do this instead of clicking the tray)

There is **no headset in the dev environment**, so most behavior is validated through headless
CLI self-test modes that log to `%AppData%\AutoVRVD\logs`. Prefer these; read the log to verify.

```powershell
AutoVRVD.exe --selftest-vda          # SAFE: create 4K120 display, confirm, remove (no topology change)
AutoVRVD.exe --selftest-restore      # SAFE: snapshot topology + re-apply unchanged (validates CCD)
AutoVRVD.exe --selftest-fullcycle    # DISRUPTIVE: full activate -> 4s -> restore (monitors go DARK)
AutoVRVD.exe --diagnose [seconds]    # detection calibration capture (needs the user's headset)
AutoVRVD.exe --selftest-settings     # construct the settings form (catches layout exceptions)
```

⚠️ **`--selftest-fullcycle` blacks out all physical monitors for ~4s.** It auto-restores and has
safety nets, but **ask the user before running it** while they're at the machine. The other
selftests are non-disruptive.

Logs are the primary debugging tool — log liberally via `Log.Info/Warn/Error`. `GdiInterop.DescribeAll()`
dumps every display + mode and is the quickest way to see topology state in the log.

## Architecture / code map

Flow: **detector → orchestrator → session → (SUDOVDA + display topology)**.

```
src/AutoVRVD/
  Program.cs                 Entry point. CLI selftest branches (BEFORE the tray), single-instance
                             mutex, DPI/visual-styles, builds & wires tray + session + orchestrator.
  Config.cs                  Config/ResolutionConfig/DetectionConfig; JSON at %AppData%\AutoVRVD\config.json.
  Paths.cs                   %AppData%\AutoVRVD paths (AppDir, LogsDir, ConfigPath, StatePath).
  Logging.cs                 `Log` static (file + in-memory ring). No NuGet.
  Autostart.cs               HKCU\...\Run toggle (no admin).
  Orchestrator.cs            State machine: detector events -> session Activate/Deactivate, gated by
                             AutomationEnabled + the Apollo-contention guard. Updates tray status.
  VirtualDisplaySession.cs   The worker: Activate (Capture -> CreateDisplay -> persist -> Apply),
                             Deactivate (Restore -> RemoveDisplay -> delete state), RecoverIfNeeded.

  Interop/Native.cs          LUID, POINTL, RECT, CreateFileW, DeviceIoControl.

  VirtualDisplay/  (Layer A — the SUDOVDA driver)
    SudoVdaInterop.cs        Interface GUID, IOCTL codes (CTL_CODE), structs, SetupAPI OpenDevice,
                             by-value Ioctl<TIn,TOut> helpers. Mirrors sudovda-ioctl.h.
    SudoVdaController.cs      Open/CreateDisplay/RemoveDisplay/SetRenderAdapter/Ping + watchdog start.
    WatchdogPinger.cs        Background ping thread (Timeout/3 ms); fires onFail after 3 misses.

  Display/  (Layer B — desktop topology)
    CcdInterop.cs            CCD API (QueryDisplayConfig/SetDisplayConfig/DisplayConfigGetDeviceInfo)
                             + all DISPLAYCONFIG_* structs. Query/QueryActive/QueryAll, name resolution
                             (FindGdiNameForTarget/GetSourceGdiName), ExtendAllDisplays.
    GdiInterop.cs            Legacy GDI: DEVMODE, EnumDisplayDevices/Settings, ChangeDisplaySettingsEx,
                             DescribeAll (diagnostics), HasActiveDisplayMatching (contention guard).
    DisplayManager.cs        Capture (snapshot), Apply (ApplyExclusive / ApplyPrimaryKeepOthers + legacy
                             fallback), Restore (re-apply snapshot w/ fallbacks), SetMode/SetPrimary.
    DisplaySnapshot.cs       Serializable topology (CCD arrays as base64) for revert + crash recovery.

  Detection/
    VdSignals.cs             Streamer process, BodyState MMF, BodyStateEvent pulse, VD-port LAN peer.
    VdSessionDetector.cs     Debounced poller -> Connected/Disconnected events.
    DetectionDiagnostics.cs  Calibration: logs every signal @250ms to a report file.

  UI/
    TrayApp.cs               NotifyIcon + context menu; events; marshals to UI thread via a hidden control.
    StatusIcons.cs           AppStatus enum + runtime-generated colored-dot icons.
    SettingsForm.cs          Settings dialog over Config (+ Autostart).
```

Data/runtime files (all under `%AppData%\AutoVRVD\`): `config.json`, `logs\autovrvd-*.log`,
`logs\diagnostics-*.log`, `session.state.json` (present only while a session is active / after a crash).

## Critical constraints & gotchas (read before changing display or driver code)

1. **Win11 24H2 (build 26200) breaks legacy display reconfig.** `ChangeDisplaySettingsEx` with
   `CDS_SET_PRIMARY` / detach **silently no-ops** (returns success, changes nothing, mangles refresh
   rates). **Use the CCD `SetDisplayConfig` API.** The proven path is `DisplayManager.ApplyExclusive`
   (supply a single active path → the virtual display becomes the sole primary). Legacy
   `SetPrimary`/`DisableAllExcept` exist only as a fallback for other Windows builds.

2. **A hot-added SUDOVDA display comes up INACTIVE on repeat adds.** Because we use a *stable*
   `MonitorGuid`, Windows remembers the monitor's last state; after the first restore it's remembered
   as "off", so the next `CreateDisplay` attaches it inactive. Therefore: **find it with
   `CcdInterop.QueryAll` (QDC_ALL_PATHS), not active-paths-only, and activate it with INVALID mode
   indices** (OS picks the mode), which `ApplyExclusive` does. `CreateDisplay` returning an empty
   `GdiName` is EXPECTED and not an error — `Apply` activates + resolves it.

3. **Keep CCD structs blittable.** Model Win32 `BOOL` as `uint`, never `bool`. The path/mode arrays
   are serialized (base64) and pointer-marshaled; a `bool` field breaks both. (`DisplaySnapshot` uses
   `MemoryMarshal` over the raw bytes.)

4. **SUDOVDA interop layout is exact** (`SudoVdaInterop`): `ADD_PARAMS` is `Pack=4` with
   `fixed byte[14]` DeviceName/SerialNumber and **integer-Hz** `RefreshRate`; `PROTOCOL_VERSION` is
   `Pack=1` (C++ `bool` → C# `byte`). IOCTLs = `CTL_CODE(FILE_DEVICE_UNKNOWN=0x22, fn, 0, 0)`.
   Interface GUID `{e5bcc234-1e0c-418a-a0d4-ef8b7501414d}`, hardware id `root\sudomaker\sudovda`.
   Installed driver protocol is **0.2.1**; verify with `--selftest-vda` after interop changes.

5. **The watchdog is the crash safety net.** SUDOVDA auto-removes the display ~3s after pings stop
   (`WatchdogPinger`, interval = `Timeout/3`). Don't block the ping thread for long. Crash recovery
   also relies on the persisted `session.state.json` (`VirtualDisplaySession.RecoverIfNeeded`, called
   at startup) — keep writing it *before* changing topology and deleting it after restore.

6. **Run in the interactive user session** (`app.manifest` = `asInvoker`). Display changes and VD's
   named objects are unreliable from session 0 — do NOT turn this into a Windows Service. The SUDOVDA
   device SD grants Everyone read/write, so no elevation is needed. If a CCD/IOCTL call ever returns
   ACCESS_DENIED, the manifest fallback is `requireAdministrator`.

7. **DPI is set in code** (`Application.SetHighDpiMode(PerMonitorV2)`); the manifest deliberately
   **omits `dpiAware`** — declaring it in both places throws at startup.

8. **PowerShell 5.1 parses no-BOM UTF-8 `.ps1` files as ANSI.** Keep `.ps1` files **ASCII-only**
   (an em-dash in a string broke `build.ps1`). `.cs`/`.md` are fine (Roslyn/editors handle UTF-8);
   diagnostics report files are written with explicit UTF-8.

9. **Detection needs live (headset) calibration and is multi-signal.** The "session active" rule is
   `streamer running && (LAN-private peer on VD ports 38810-38840  ||  BodyStateEvent pulses)`. The
   public **cloud-relay** connection on 38810 (e.g. 40.x Azure) must be filtered out (`VdSignals.IsPrivate`).
   Idle baseline is verified (no false positives); the connected half is confirmed via `--diagnose`
   with the user's headset. Mode is `auto|event|ports` in config.

10. **Don't regenerate `MonitorGuid`.** Its stability is what lets Windows persist the virtual
    display's layout/identity across sessions.

## Conventions

- **Interop:** `DllImport` (not `LibraryImport`) for consistency; blittable structs; by-value IOCTL
  helpers (`unmanaged` generics). Interop types are `internal`; controllers expose `internal` methods
  when they surface interop structs (avoid public exposure → accessibility errors).
- **Threading:** controllers/session lock a private `_gate` (reentrant Monitor). The detector raises
  events on its background thread; UI updates marshal through `TrayApp`'s hidden control.
- **Logging over assertions:** there's no test project; verify via selftests + log inspection.
- **Config is read live** by the detector each poll, so most settings apply without restart
  (resolution applies on next activation).

## Shipping & self-update

The global specs in `Z:\global .md\` (`ship.md`, `updater.md`) are the authority for the process;
this repo implements the .NET equivalents. **Repo:** `robogears/AutomaticVRVDCreator`. **Asset:**
`AutoVRVD-win-x64.exe` (single-file, self-contained — the substring `win-x64.exe` is what the
updater matches; don't break it).

**In-app updater** (`Update/`): `Updater.cs` polls `releases/latest`, compares the assembly version
to the tag (numeric), downloads the win-x64 asset, and swaps-and-relaunches via a detached `.cmd`
(`ApplyUpdate`). `UpdateController.cs` drives the tray menu state machine. Launch check is silent
(`UpdateCheckOnLaunch`). The `Owner`/`Repo` constants in `Updater.cs` are hardcoded — update them if
the repo is renamed.

**Ship process — NEVER auto-ship; only on explicit "ship it / release vX.Y.Z".** Follow `ship.md`:
1. Bump `<Version>` in `src/AutoVRVD/AutoVRVD.csproj` (patch by default). CI passes this to the build
   via the tag, so the shipped exe's version == the tag (the updater relies on this).
2. Overwrite `RELEASE_NOTES.md` entirely with the new version's body — keep the 4-part format
   (What's new / `---` / Install + Requirements / `---` / Full Changelog compare link). No old sections.
3. `git add` explicitly, commit, `git tag -a vX.Y.Z -m vX.Y.Z`, push main, push the tag.
4. The tag triggers `.github/workflows/release.yml` → builds the single-file exe → attaches it to a
   **draft** release via `softprops/action-gh-release@v2`.
5. Run `.\ship-tail.ps1 vX.Y.Z` — waits for CI, **verifies the release body** (set from
   `RELEASE_NOTES.md` if empty — a known softprops quirk), and leaves it as a draft.
6. The user reviews and publishes manually (`gh release edit vX.Y.Z --draft=false`). Never flip
   `draft` in the workflow.

The GitHub repo does not exist / has no remote yet — creating it and the first push are the user's
call (don't push or create it without an explicit instruction). `gh` is authenticated as `robogears`.

## Status (2026-06-01)

All six build milestones complete and tested **except live headset calibration** (a user step). The
full create → 4K120-primary → disable-others → restore cycle is verified end-to-end on the dev
machine via `--selftest-fullcycle`. Not yet committed to git (on `main`). Possible follow-ups: lock
`Detection.Mode` to the proven signal after calibration; optional DXGI GPU-LUID picker in Settings.
