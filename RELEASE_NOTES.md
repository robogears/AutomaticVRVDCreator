# What's new in v0.1.6

## Update checks now show a window
- Clicking **Check for updates** in the tray used to close the menu and run the check invisibly — you couldn't see "Checking…", the result, or the download progress. Now it opens a small **Updates window** that stays open and shows live status: **Checking → Up to date / Update available → Downloading % → Restart & install**, with a clear action button. No more guessing whether a check actually ran.

---

# Install

- **Recommended — installer:** download **`VirtualMirage-Setup.exe`** and run it. SmartScreen may warn on the unsigned build: **More info -> Run anyway**.
- **Portable alternative:** **`VirtualMirage-win-x64.exe`** is a self-contained single exe you can run from anywhere (no install, no auto-update).
- **Updating from v0.1.5:** the in-app updater will fetch this for you — tray -> **Check for updates** -> the new window walks you through it.

Config and logs live in `%AppData%\VirtualMirage\`.

## Requirements

- Windows 10/11 x64.
- **Virtual Desktop Streamer 1.30+** running on the PC.
- The **SUDOVDA** virtual display driver installed and started (bundled with Apollo, or standalone from <https://github.com/SudoMaker/SudoVDA>).

---

**Full Changelog**: https://github.com/robogears/VirtualMirage/compare/v0.1.5...v0.1.6
