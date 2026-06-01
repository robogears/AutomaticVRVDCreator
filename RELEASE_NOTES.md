# What's new in v0.1.1

First public release.

## Automatic 4K120 virtual display for Virtual Desktop
- Detects when you connect to your PC in VR via **Virtual Desktop** and automatically creates a **3840×2160 @ 120 Hz** virtual display (SUDOVDA), makes it primary, and disables your physical monitors.
- Restores your exact desktop layout the moment you disconnect.
- **Keeps the resolution locked at 4K120** even if Virtual Desktop tries to drop the display to its lower stream resolution after connecting — no more manually fixing it in Windows settings every time.
- Crash-safe: a driver watchdog removes the virtual display if the app dies, and your previous layout is restored on the next launch.

## Detection & control
- Multi-signal Virtual Desktop session detection (streamer process + LAN streaming ports + BodyState event) with a one-time calibration diagnostics tool.
- Tray app: status icon, manual create/remove, settings (resolution, detection mode, monitor handling), and run-at-login.

## Self-update
- Checks GitHub for new releases on launch and can download + apply them from the tray menu ("Update available… → Restart to apply").

---

# Install

- **Windows (x64)**: download `AutoVRVD-win-x64.exe` and run it. It's self-contained — no .NET install needed. On first launch SmartScreen may warn (the build is unsigned): click **More info → Run anyway**. A tray icon appears.

Config and logs live in `%AppData%\AutoVRVD\`.

## Requirements

- Windows 10/11 x64.
- **Virtual Desktop Streamer 1.30+** running on the PC.
- The **SUDOVDA** virtual display driver installed and started (bundled with Apollo, or standalone from <https://github.com/SudoMaker/SudoVDA>).

---

**Full Changelog**: https://github.com/robogears/AutomaticVRVDCreator/commits/v0.1.1
