# Nordlys

A small offline Windows app that teaches game development with **papers** (lecture-style readings) and **practice questions** (one per screen, with hints, explanations, review scheduling and mastery levels).

The app does not write lessons. You ask Claude for a lesson in a normal claude.ai chat, copy what it gives you, and import it. No account, no API key, no internet connection needed by the app.

## Install

1. Run `desktop\publish\Nordlys-0.2.1-setup.exe` (after building, see below). It needs no admin rights.
2. Open **Nordlys** from the Start menu.

If the earlier version (0.1.0) is installed, this replaces it in place. Your lessons and progress are kept: they live in a separate folder that the installer and uninstaller never touch.

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

You can also drop a `.lesson.md` file onto the Import screen, or use **Open a .lesson.md file…** (Ctrl+O).

### Right-click menus
Right-click (or press the Menu key / Shift+F10) on anything for what you can do with it:
- **Selected text** in a lesson: Copy, Search the web, Look up in the Glossary (if it is a term).
- **A code block**: Copy code block.
- **A question**: Copy question, Show hint, Show answer, Copy explanation (once answered).
- **A lesson in the Library**: Open, Practise, Duplicate, Open file in editor, Show in Explorer, Reset progress…, Delete lesson… (to the Recycle Bin).
- **A unit on the Map**: open its lessons, Ask Claude for a lesson on this unit, Copy unit id.
- **A term in the Glossary**: Copy term / definition, Open the lesson it came from.
- **Any text box**: Undo, Cut, Copy, Paste, Select all (only the ones that can run right now).

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
| F5 | Reload the lesson or library from its files |
| F11 | Fullscreen on / off |
| Alt+F8 | Resize the window with the arrow keys (Shift = bigger steps, Enter = apply, Esc = cancel) |
| Alt+Space | The window menu (Restore, Move, Size, Minimise, Maximise, Close) |

### Window
- It behaves like any Windows app: resize from any edge or corner, snap by dragging the title bar to a screen edge or with Win+arrow keys, hover the maximise button for Snap Layouts (Windows 11), right-click the title bar for the window menu, double-click it to maximise.
- **Marquee resize**: drag the small grip in the bottom-right corner. A translucent blue rectangle shows the new size (the number is in its corner); let go to apply, Esc to cancel. Settings → Resize preview changes how see-through it is.
- The window reopens at the same size, position and maximised state.

### Where your data is
`%APPDATA%\app.nordlys\` (Settings → **Open data folder**):
- `lessons/` one `.lesson.md` file per lesson
- `progress.json` everything you have answered (a backup is made each day in `backups/`, the last 14 are kept)
- `curriculum.json` and `generator-prompt.md` your own copies; edit them if you want. Settings → **Reset generator prompt to default** restores the original prompt.

## Build from source

Needs the .NET 10 SDK. From the `desktop` folder:

```bash
dotnet test --project Nordlys.Tests
dotnet run --project Nordlys.App
dotnet publish Nordlys.App -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish\folder
```

Then build the installer with NSIS: `makensis /DVERSION=0.2.1 installer\nordlys.nsi`.

See `CLAUDE.md` for how the code is organised and `DECISIONS.md` for choices made while building.
