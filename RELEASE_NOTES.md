# What's new in v0.1.12

## Fix: virtual display not appearing after saving a VR layout
- In v0.1.11, once you'd saved a VR layout, the virtual display could be **created but left inactive (invisible)** on connect. Cause: it tried to replay the exact saved topology, but the virtual display's internal ID changes every session, so the replay failed and never activated the new display. VirtualMirage now **reconstructs** your saved VR layout against the current display, so the virtual reliably appears (with a fallback to standard activation as a safety net).

---

# Install

- **Recommended — installer:** download **`VirtualMirage-Setup.exe`** and run it — no admin, no UAC. SmartScreen may warn on the unsigned build: **More info -> Run anyway**.
- **Portable alternative:** **`VirtualMirage-win-x64.exe`** is a self-contained single exe (no install, no auto-update).
- **Updating from v0.1.8+:** tray -> **Check for updates -> Download & install** (silent, no prompts).

Config and logs live in `%AppData%\VirtualMirage\`.

## Requirements

- Windows 10/11 x64.
- **Virtual Desktop Streamer 1.30+** running on the PC.
- The **SUDOVDA** virtual display driver installed and started (bundled with Apollo, or standalone from <https://github.com/SudoMaker/SudoVDA>).

---

**Full Changelog**: https://github.com/robogears/VirtualMirage/compare/v0.1.11...v0.1.12
