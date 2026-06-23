# What's new in v0.1.10

## Fix: works with "Disable my physical monitors" turned OFF
- The virtual display used to activate **only** when *"Disable my physical monitors while in VR"* was on. With it **off**, the virtual display silently failed to launch — and a monitor you'd disabled could get switched back on. Now it reliably comes up **alongside** your physical monitors (and becomes primary), so you can keep an external monitor on while gaming — e.g. to show the game on it with **Win+P → Duplicate**. Monitors that were already off stay off, on both activate and restore.

---

# Install

- **Recommended — installer:** download **`VirtualMirage-Setup.exe`** and run it — no admin, no UAC. SmartScreen may warn on the unsigned build: **More info -> Run anyway**.
- **Portable alternative:** **`VirtualMirage-win-x64.exe`** is a self-contained single exe you can run from anywhere (no install, no auto-update).
- **Updating from v0.1.8+:** tray -> **Check for updates -> Download & install** (silent, no prompts).

Config and logs live in `%AppData%\VirtualMirage\`.

## Requirements

- Windows 10/11 x64.
- **Virtual Desktop Streamer 1.30+** running on the PC.
- The **SUDOVDA** virtual display driver installed and started (bundled with Apollo, or standalone from <https://github.com/SudoMaker/SudoVDA>).

---

**Full Changelog**: https://github.com/robogears/VirtualMirage/compare/v0.1.9...v0.1.10
