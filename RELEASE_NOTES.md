# What's new in v0.1.2

## Faithful monitor restore (the big fix)
- Your monitors now return to their **exact** pre-VR state on disconnect — resolution, **refresh rate**, position, and **which one is primary**.
- Fixes the bug where refresh dropped (e.g. **360 Hz → 120 Hz**) and your **secondary monitor became primary** after exiting VR. Root cause: when other virtual-display drivers (Sunshine, Meta, Virtual Desktop's own) were active, the restore fell back to Windows' last-remembered config and lost your real settings — and it compounded each session.
- AutoVRVD now snapshots each **physical** monitor's exact mode before switching and re-applies it explicitly on disconnect (and after a crash), independent of any other virtual displays on your system.

> One-time note: if your monitors are currently stuck at 120 Hz / wrong primary from the old bug, set them back to native once in Windows Display Settings — from then on AutoVRVD keeps them there.

---

# Install

- **Windows (x64)**: download `AutoVRVD-win-x64.exe` and run it (self-contained — no .NET install needed). Existing users get this automatically via the tray's update check. On first launch SmartScreen may warn (unsigned): **More info → Run anyway**.

Config and logs live in `%AppData%\AutoVRVD\`.

## Requirements

- Windows 10/11 x64.
- **Virtual Desktop Streamer 1.30+** running on the PC.
- The **SUDOVDA** virtual display driver installed and started (bundled with Apollo, or standalone from <https://github.com/SudoMaker/SudoVDA>).

---

**Full Changelog**: https://github.com/robogears/AutomaticVRVDCreator/compare/v0.1.1...v0.1.2
