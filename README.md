# Nordlys

A small offline desktop app that teaches game development with **papers** (lecture-style readings) and **practice questions** (one per screen, with hints, explanations, review scheduling and mastery levels).

The app does not write lessons. You ask Claude for a lesson in a normal claude.ai chat, copy what it gives you, and import it. No account, no API key, no internet connection needed by the app.

## Install

1. Run the installer: `src-tauri/target/release/bundle/nsis/Nordlys_0.1.0_x64-setup.exe` (after building, see below).
2. Click through the installer. It needs no admin rights.
3. Open **Nordlys** from the Start menu.

The first time it opens, it copies three sample lessons, the curriculum and the generator prompt into its data folder.

## Use

### The daily loop
1. On **Home**, press **Next** (or Enter).
2. Pick your energy: **1** (5–10 min), **2** (~25 min) or **3** (45–90 min).
3. Read the paper. "Done means" at the top says exactly when you are finished.
4. Press **Start practice** (or P). Answer each question; **Check answer** with Enter.
5. On the summary, press **Mark lesson done**.
6. When questions are due again, Home shows **Review (N due)**.

### Getting a new lesson (the 4-step loop)
1. **Ask**: type what you want to learn (or leave it empty for "next in curriculum"), pick your energy, press **Copy prompt for Claude**.
2. **Paste into Claude** in a new chat on claude.ai and send it.
3. **Copy the lesson** Claude gives you (the whole reply is fine; chat text around the lesson is removed).
4. **Import**: paste it and press **Import lesson** (Ctrl+Enter). If there are errors, press **Copy error report**, paste it back to Claude in the same chat, and import the corrected lesson.

You can also drop a `.lesson.md` file onto the Import screen.

### Keyboard
| Key | Does |
|---|---|
| G then H / L / M / R / A / I | Go to Home / Library / Map / Review / Ask / Import |
| Esc | Back |
| 1–6 | Pick a choice |
| Enter | Check answer, then Next question |
| H | Show a hint |
| Space, then ↑ / ↓ | Pick up and move an item (order questions) |
| Ctrl+Enter | Show model answer (reflect questions) · Import · Copy prompt |

### Where your data is
`%APPDATA%\app.nordlys\` (Settings → **Open data folder**):
- `lessons/` one `.lesson.md` file per lesson
- `progress.json` everything you have answered (a backup is made each day in `backups/`, the last 14 are kept)
- `curriculum.json` and `generator-prompt.md` your own copies; edit them if you want. Settings → **Reset generator prompt to default** restores the original prompt.

## Build from source

Needs Node LTS, Rust (rustup) and the Visual Studio C++ build tools.

```bash
npm install
npm run dev       # web version in a browser: http://localhost:1420
npm run app       # desktop window, with live reload
npm test          # unit tests
npm run release   # installer in src-tauri/target/release/bundle/nsis/
```

See `CLAUDE.md` for how the code is organised and `DECISIONS.md` for choices made while building.
