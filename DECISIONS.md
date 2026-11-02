# Decisions

Choices made while building that the handoff did not settle, or where the build differs from `HANDOFF.md`.

## Toolchain and shell
- **MSVC from Visual Studio 2026 Community.** It was already installed, so no separate Build Tools were needed. Node 26 (current LTS from winget) and Rust stable via rustup.
- **TypeScript 7, Vite 8, Vitest 5.** These were the latest stable versions at build time.
- **App identifier `app.nordlys`.** So the data folder (Tauri `AppData`) is `%APPDATA%\app.nordlys\`, not `<AppData>/nordlys/`. The layout inside it matches the handoff.
- **Added `tauri-plugin-opener`** for the Settings button "Open data folder". The three listed plugins can't open a folder in Explorer.
- **No file-picker dialog for import.** Import uses a standard HTML file input and HTML drag-and-drop (window `dragDropEnabled: false`). This works the same in Tauri and in the browser, and the fs plugin never needs access outside the data folder. The dialog plugin is only used for "Export progress backup" (a save dialog).
- **Content Security Policy** blocks all network access (`default-src 'self'`). This enforces "no network calls".
- **Installer type: NSIS** (`.exe`) only. MSI needs WiX and adds nothing for a single user.
