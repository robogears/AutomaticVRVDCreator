# AutoVRVD — Automatic VR Virtual Display Creator

A lightweight Windows tray app that detects when you connect to your PC in VR through
**Virtual Desktop**, and automatically:

1. creates a **4K (3840×2160) @ 120 Hz** virtual display (via the **SUDOVDA** driver),
2. makes it the **primary** display and **disables your physical monitors**, then
3. **restores everything exactly** when you disconnect.

So you can play flat games at 4K120 on a headless virtual screen inside Virtual Desktop —
the same virtual-display lifecycle Moonlight/Apollo use when a stream starts/stops, but
triggered by *Virtual Desktop* connecting instead.

---

## Requirements

- **Windows 10/11 x64** (developed/tested on Windows 11 build 26200 / 24H2 branch).
- **Virtual Desktop Streamer ≥ 1.30** running on the PC.
- **SUDOVDA** ("SudoMaker Virtual Display Adapter") driver installed and *Started*.
  - If you have **Apollo** installed, you already have it (`C:\Program Files\Apollo\drivers\sudovda\`).
  - Otherwise install it standalone from <https://github.com/SudoMaker/SudoVDA> (it's code-signed; no test-signing needed).
- **.NET 8 Desktop Runtime** (to run). To build, the **.NET 8 SDK**.

No administrator rights are required — the SUDOVDA device grants user read/write, and
display changes are made in your interactive session.

---

## Quick start

**Easiest — download the release:** grab `AutoVRVD-win-x64.exe` from the
[Releases page](https://github.com/robogears/AutomaticVRVDCreator/releases) and run it (it's
self-contained — no .NET install needed). On first launch SmartScreen may warn (unsigned build):
**More info → Run anyway**. A tray icon appears.

**Or build from source:**
```powershell
.\build.ps1                 # Release build (see "Building" for the SDK)
.\src\AutoVRVD\bin\Release\net8.0-windows\AutoVRVD.exe
```

Then **calibrate detection once with your headset** (see below), open **Settings…** from the
tray to enable **Start with Windows**, and you're done. The app **checks for updates on launch**
and can download + apply them from the tray ("Update available… → Restart to apply").

---

## How to use

Right-click the tray icon:

| Menu item | What it does |
|---|---|
| **Automation enabled** | Master switch. When on, connecting via VD auto-activates the 4K120 display. |
| **Create virtual display now** | Manually run the full activation (create + primary + disable others). Great for testing without a headset. |
| **Remove virtual display now** | Restore your desktop and remove the virtual display. |
| **Run detection diagnostics…** | 60-second calibration capture — see below. |
| **Settings…** | Resolution/refresh, detection mode, debounce, Apollo guard, start-with-Windows. |
| **Open logs folder** | `%AppData%\AutoVRVD\logs`. |
| **Quit** | Exits (restores your desktop first if a session is active). |

The tray icon color shows state: gray = idle, blue = VR connected, green = virtual display
active, red = error.

### Calibrate detection (one-time, needs your headset)

Detecting "am I connected in VR via Virtual Desktop" is the one part that varies per setup,
so calibrate it once:

1. Tray → **Run detection diagnostics…** (or run `AutoVRVD.exe --diagnose 60`).
2. Within the 60-second window: **connect your headset** via Virtual Desktop, wait ~10s,
   then **disconnect**.
3. The report opens automatically (also in `%AppData%\AutoVRVD\logs\diagnostics-*.log`).
   Look at the `>>> raw active changed` lines and the columns:
   - if **lanPeers** shows an address when connected → **ports** mode works,
   - if **eventPulse** flips to `1` when connected → **event** mode works,
   - **auto** (default) uses whichever fires.
4. If only one signal works, set **Settings → Detection → Mode** accordingly.

---

## How it works

Two layers, mirroring Apollo's proven design:

- **Detection** (`Detection/`): gates on `VirtualDesktop.Streamer.exe`, then detects a live
  session via an **established TCP connection on the VD ports (38810–38840) to a LAN/private
  address** (the headset — the public cloud-relay connection on 38810 is filtered out) and/or
  the **`VirtualDesktop.BodyStateEvent`** pulsing. Debounced to avoid flapping.
- **Layer A — SUDOVDA** (`VirtualDisplay/`): opens the driver by its interface GUID and sends
  IOCTLs to add a 4K120 display with a **stable monitor GUID**, with a watchdog ping thread
  (the driver auto-removes the display ~3s after pings stop — the crash safety net).
- **Layer B — display topology** (`Display/`): snapshots the current topology, then uses the
  **CCD API** (`SetDisplayConfig`) to make the virtual display the sole primary 4K120 screen,
  and restores the snapshot on disconnect. (The legacy `ChangeDisplaySettingsEx` path is kept
  only as a fallback; it silently no-ops on Windows 11 24H2.)

Crash safety = two independent nets: the SUDOVDA **watchdog** (auto-removes an orphaned
display) plus a **persisted snapshot** (`%AppData%\AutoVRVD\session.state.json`) that is
restored on next launch if the app died mid-session.

---

## Configuration

`%AppData%\AutoVRVD\config.json` (editable by hand; Settings… edits the common ones):

| Key | Default | Meaning |
|---|---|---|
| `AutomationEnabled` | `true` | Master switch. |
| `Display` | `3840×2160@120` | Virtual display mode. |
| `MonitorGuid` | generated | Stable identity so Windows remembers the layout. Don't change. |
| `SetPrimary` | `true` | Make the virtual display primary. |
| `DisableOtherMonitors` | `true` | Turn off physical monitors while in VR. |
| `RenderAdapterLuid` | `null` | Optional GPU LUID to render on (null = default/primary GPU). |
| `Detection.Mode` | `auto` | `auto` \| `event` \| `ports`. |
| `Detection.DebounceConnectMs` | `1500` | Stable-for-this-long before activating. |
| `Detection.DebounceDisconnectMs` | `3000` | Stable-for-this-long before restoring. |
| `Detection.StreamerPorts` | `38810,38820,38830,38840,38850` | VD ports to watch. |
| `SkipIfApolloActive` | `true` | Don't activate if a SUDOVDA display is already active (Apollo/Sunshine stream). |
| `StartWithWindows` | `false` | Mirrors the HKCU Run entry (toggle in Settings). |

---

## CLI / diagnostics

```powershell
AutoVRVD.exe --diagnose [seconds]     # detection calibration capture (default 60s)
AutoVRVD.exe --selftest-vda           # create a 4K120 display, confirm, remove (non-disruptive)
AutoVRVD.exe --selftest-restore       # snapshot + re-apply topology unchanged (safe)
AutoVRVD.exe --selftest-fullcycle     # full activate→restore (DISRUPTIVE: monitors go dark ~4s)
```
All write to `%AppData%\AutoVRVD\logs`.

---

## Troubleshooting

- **It doesn't switch when I connect in VR.** Run detection diagnostics with your headset and
  set the Detection Mode to whichever signal fires (`ports` or `event`). Check the Streamer is
  running and is ≥ 1.30.
- **Screens didn't come back after a crash.** Relaunch AutoVRVD — it restores from the
  persisted snapshot on startup. The SUDOVDA watchdog also auto-removes the orphan display
  ~3s after the app stops. Worst case, unplug/replug or `Win+P` → Extend.
- **The virtual display isn't 4K120.** Confirm SUDOVDA supports the mode and that your config
  resolution is exactly `3840×2160@120`. Check the log for `SetMode` results.
- **It skips activation saying a display is already active.** That's the Apollo/Sunshine guard
  (`SkipIfApolloActive`). Don't Moonlight-stream and VD-connect at the same time, or disable
  the guard in Settings.
- **Windows 11 24H2 note:** legacy set-primary/disable is unreliable on this branch; AutoVRVD
  uses the CCD API specifically to work around it. If you ever see only partial switching,
  check the log and file an issue with it.

---

## Building

Requires the **.NET 8 SDK**. If you only have the runtime:

```powershell
# user-local install, no admin needed:
irm https://dot.net/v1/dotnet-install.ps1 -OutFile $env:TEMP\dotnet-install.ps1
& $env:TEMP\dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
# or machine-wide: winget install Microsoft.DotNet.SDK.8
```

Then:

```powershell
.\build.ps1            # Release build
.\build.ps1 -Publish   # single-folder publish to .\publish
```

Project layout: `src/AutoVRVD/` — `Detection/`, `VirtualDisplay/` (SUDOVDA), `Display/` (CCD +
legacy GDI), `UI/` (tray + settings), `Orchestrator.cs`, `VirtualDisplaySession.cs`.

---

## Credits / prior art

- **SUDOVDA** by SudoMaker / ClassicOldSong — the virtual display driver.
- **Apollo** (Sunshine fork) and **libdisplaydevice** by LizardByte — the virtual-display
  lifecycle and topology-restore patterns this app mirrors.
