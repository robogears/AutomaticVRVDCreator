# What's new in v0.1.5

## New name: VirtualMirage
- AutoVRVD is now **VirtualMirage** — same app, better name. Everything is renamed: the app and tray, the installer (**`VirtualMirage-Setup.exe`**), and the project/repo. Your existing settings — including the virtual display's **stable identity** — carry over automatically the first time VirtualMirage runs (it migrates `%AppData%\AutoVRVD` -> `%AppData%\VirtualMirage`).

## Upgrading from AutoVRVD
- The in-app updater on your old AutoVRVD build will fetch this release and install VirtualMirage. Because it's a renamed product, the old **AutoVRVD** entry stays in Add/Remove Programs — **uninstall it** once VirtualMirage is up. (Or just download `VirtualMirage-Setup.exe` below and run it.)

---

# Install

- **Recommended — installer:** download **`VirtualMirage-Setup.exe`** and run it. SmartScreen may warn on the unsigned build: **More info -> Run anyway**.
- **Portable alternative:** **`VirtualMirage-win-x64.exe`** is a self-contained single exe you can run from anywhere (no install, no auto-update).

Config and logs live in `%AppData%\VirtualMirage\`.

## Requirements

- Windows 10/11 x64.
- **Virtual Desktop Streamer 1.30+** running on the PC.
- The **SUDOVDA** virtual display driver installed and started (bundled with Apollo, or standalone from <https://github.com/SudoMaker/SudoVDA>).

---

**Full Changelog**: https://github.com/robogears/VirtualMirage/compare/v0.1.4...v0.1.5
