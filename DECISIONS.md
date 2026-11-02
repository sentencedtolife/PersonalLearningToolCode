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

## Lesson format, parser, validator (M1)
- **Line numbers** in validation messages count from the first line of the lesson itself (the `---`), after any outer fence and chat text are removed. That matches the lesson Claude wrote, so the error report makes sense when pasted back.
- **Field lines** are found by scanning for `key:` at the start of a line. Errors about a field inside a list (e.g. one bad choice) point at the list's key line. Missing fields point at line 1 (frontmatter) or at the `~~~question` line.
- **Extra warnings** (never errors): unknown frontmatter or question fields (catches typos), a unit that belongs to a different track, a duplicate glossary term, `feedback` on the correct answer, a `fill` blank written as plain text instead of a list (it is accepted as a one-item list), no `## Practice` heading, no questions.
- **Plain-text checks**: list items (choices, items, accept, blanks, pairs) may be numbers or true/false in YAML; they are read as text. An item that YAML reads as a map (e.g. an unquoted `Title: options`) is an error that tells you to add quotes.
- **`fill` accepts `case_sensitive`** too, because the handoff says blanks use "the same rules as `text`".
- **Track `K` (capstones)**: units K.onebutton, K.arcade, K.puzzle, K.jam, K.story, with tiers 3–4 and no prerequisites. The handoff gives no tiers or prerequisites for them.
- **Core Loops sample**: the handoff example's "Explore → fight → …" became "Search a dungeon → fight → …", because "explore" is a banned word in the generator prompt and a test checks the bundled lessons for banned words.
- **Contract tests**: `tests/content.test.ts` checks that `content/generator-prompt.md` matches HANDOFF.md exactly, that the prompt's own example lesson validates cleanly, and that the bundled lessons follow the prompt's sizing and question-mix rules.
