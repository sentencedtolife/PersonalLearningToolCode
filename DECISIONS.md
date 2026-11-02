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

## Paper view and question engine (M2, M3)
- **M2 and M3 share one commit.** The paper's inline "Check yourself" cards are built from the question engine, so the two milestones landed together.
- **Inline "Check yourself" cards are self-checks only.** They don't write progress. All questions, including the inline ones, are recorded when answered in Practice mode, so nothing is counted twice.
- **Practice saves after every answer** (Leitner box, history, the run's results). Closing the app mid-practice resumes at the first unanswered question.
- **The attempt is recorded when the summary appears.** "Mark lesson done" then sets the status to practiced, logs the session (minutes since the lesson was opened, capped at 240) and returns Home. "Practice again" starts a fresh run.
- **Answering a question that already has a Leitner box** (e.g. practising a lesson a second time) uses the review rule. The first-practice rule applies only to the very first answer.
- **After the second wrong try** the card shows "Here is the answer" in Frost blue (nord9), plus the explanation. No red anywhere in questions.
- **`match`** uses one dropdown per left item; the dropdown options are the right column, shuffled. This is fully keyboard-accessible.
- **`order`** keyboard use: focus an item, press Space to pick it up, ↑/↓ to move it, Space to drop. Mouse: drag, or the ↑/↓ buttons on each item.
- **"Hint (N left)"** only appears when the question has hints.
- **Last question's button says "See summary"** instead of "Next question".
- **Glossary terms** are underlined at every use; only the first use of each term is in the Tab order, so Tab doesn't stop on the same word many times.
- **Web links in lessons** open in the system browser (opener plugin, http/https only). The app window never navigates away.
- **Lesson HTML** may contain inline HTML, but `<style>`, `<script>`, forms, inputs and iframes are removed by DOMPurify. `$5 and $10` is left as money: the closing `$` of inline math must follow a non-space and not be followed by a digit.

## Progress, review and mastery (M4)
- **"In progress"** means opened but not yet marked done (status `read`). Next resumes the most recently opened one; Continue and Next reopen the lesson in the mode it was left in (paper or practice).
- **Backups**: the first save of each day copies the current `progress.json` (the end state of the last day the app was used) to `backups/progress-YYYY-MM-DD.json`; the 14 newest are kept. A damaged `progress.json` is kept as `backups/progress-damaged-YYYY-MM-DD.json` before the newest readable backup is loaded, and Home shows a one-line notice.
- **Mastery "averaging"** is checked over any run of consecutive qualifying answers (5 for Proficient, 8 for Mastered), in date order. So a level, once reached, stays reached ("levels only go up"), and early mistakes don't block a level forever. Any answer to a question at least 2 (or 7) days after its `firstSeen` counts as a review answer, whether it came from Review or from practising the lesson again.
- **"Not started"** also covers a unit whose lessons were practised but never scored 70%+. There is no separate level for that.
- **Mastery hover text** on the Map shows the rule for the current level and for the next one.
- **Extra progress fields**: `lessons[id].imported` (for "oldest imported first"), `lessons[id].lastOpened` (for "continue the most recent"), and `lessons[id].run` (a practice run in progress). `lastPosition` is `{view, scroll}`.

## Prompt loop (M5)
- **Copied prompt** = `generator-prompt.md` (the user's copy in the data folder) + the learner context block + a last line `My request: …` (or "next …" when the box is empty). The request also appears inside the context block, as the handoff's format shows.
- **Map → Ask** pre-fills the request as "Teach me the unit P.signals (Signals)." Home's "No lesson ready" passes the chosen energy.
- **Clipboard in browser mode** falls back to the old `execCommand('copy')` when the Clipboard API is blocked. The desktop app uses the clipboard plugin.
- **Import saves the cleaned text** (no chat text, no outer fence, `\n` line endings) as `lessons/<id>.lesson.md`. The paste box is cleared after a successful import.
- **Drag-and-drop or the file button imports the first file only.**
- **Errors and warnings are sorted by line.** Issues about the whole lesson (no questions, no Practice heading) show "whole lesson" instead of a line number.
- **Keep both** rewrites only the frontmatter `id:` line to the first free `<id>-2`, `<id>-3`, …

## Polish (M6)
- **Glossary screen** lists the terms from lessons you have opened (that is when a lesson's terms become "known" and enter `known_terms` in the prompt context).
- **Library filters** (track, status) stay as set while the app is open; they reset on restart. Lesson files that fail validation are not listed; a notice names them.
- **"Reset generator prompt to default"** asks for a second click ("Yes, reset it") in place, because it overwrites an edited file. No popup.
- **Light theme** darkens yellow and green slightly (`--not-yet`, `--correct`) so they stay readable on Snow Storm backgrounds.

## Performance and window overhaul (after M7)
Measured before and after with the same methods (details in the commit messages):
- **Lazy lesson stack.** Startup JS 581 KB → 146 KB (193 → 49 KB gzipped). Home ready in the browser build: 28.6 → 16.8 ms median.
- **Saves**: coalesced, and the daily-backup and folder checks run once. A steady-state save went from 5 file operations to 1 write (tested by counting store calls).
- **Paper cache** per lesson object (WeakMap). Opening a lesson: 13.8 → 7.3 ms median.
- **Window state** via the official `tauri-plugin-window-state` (size, position, monitor, maximised, fullscreen). Decorations and visibility are not restored. Off-screen positions (an unplugged monitor) are not restored; the plugin handles that.
- **Marquee resize**: a grip in the bottom-right corner, plus Alt+F8 for the keyboard (Alt+F8 is the KDE and Windows "resize window" shortcut). The window's top-left stays put. While dragging, the window grows live so the preview can extend past the old edge, but it only shrinks on release, so the rectangle is always visible. Limits: the 720×520 minimum, and the work area of the window's monitor (computed in physical pixels, so mixed DPI is handled). The grip is hidden when the window is maximised or fullscreen. The preview is Frost blue (nord9), with a Light/Medium/Strong opacity setting.
- **F11** toggles fullscreen.
- **Accent colours** from Frost and Aurora only. Red and yellow are excluded because they mean "error" and "not yet". In the light theme, accents are darkened with `color-mix` for contrast; the default stays nord10, as before.
- **Density** setting (Comfortable / Compact) via spacing tokens.
- **Screen fade**: 120 ms, opacity + transform only. It respects reduced motion.
- **Not done**: window translucency (Mica/Acrylic). It needs a transparent window, which on Windows drops the frameless window's shadow and rounded corners, and it adds compositing work for little benefit.

## Manual checklist
Checked in the browser version during the build (2026-11-02):
- [x] Keyboard only: G-chords, Next → 1/2/3, choices 1–6, Enter to check, H for hint, Space + ↑/↓ for order, Ctrl+Enter for reflect and import, Esc for back.
- [x] Light and dark themes, serif paper font, font sizes, reduce motion.
- [x] Resume after restart: reopening with no route goes to the last screen; lesson scroll position and a half-finished practice run are restored.
- [x] Corrupt progress.json → newest valid backup + notice (unit test).

To check in the desktop window (`npm run app` or the installed app):
- [ ] Window controls: minimise, maximise/restore (icon changes), close (turns red on hover), drag by the header, double-click the header to maximise.
- [ ] "Open data folder" opens `%APPDATA%\app.nordlys` in Explorer.
- [ ] "Export progress backup" shows a save dialog and writes the file.
- [ ] Copy prompt for Claude → paste into claude.ai works.
- [ ] Marquee resize: drag the bottom-right grip bigger and smaller; Esc cancels mid-drag; Alt+F8 + arrows + Enter.
- [ ] Snap: drag the title bar to a screen edge; Win+←/→.
- [x] Window memory: size, position and maximised state restored after closing (scripts/test-window-state.ps1).

## Build (M7)
- **Release build**: `Nordlys_0.1.0_x64-setup.exe` is 2.61 MiB (target: under 15 MB); `nordlys.exe` is 5 MB. Rust is built with LTO, `opt-level = "s"`, `strip`.
- **Installer** is per-user (Tauri's NSIS default), so it needs no admin rights. WebView2 is already part of Windows 11.
- Checked after building: the release exe opens a window titled Nordlys, creates `%APPDATA%\app.nordlys\`, copies the bundled content into it, and writes `progress.json` and the daily backup.

## Native rewrite (WPF), replacing the Tauri/WebView app
Elliott asked for a native Windows app without an embedded browser, and chose a full rewrite over hardening the WebView app.
- **WPF on .NET 10**, not WinUI 3 or a Rust GUI. WPF's `FlowDocument` handles the papers: tables, inline controls (question cards, fill-in blanks inside code), tooltips, selectable text. Mature libraries exist for LaTeX (WpfMath) and SVG (SharpVectors). WinUI 3's rich text has no tables and no maths renderer. Slint/egui draw their own widgets, so they are not native controls.
- **Same data, same contract.** `progress.json` and `settings.json` keep the same JSON shape, and the data folder is still `%APPDATA%\app.nordlys`, so existing progress loads; a test reads a file written by the web version. The validator's messages are ported word for word. YAML is resolved like js-yaml's core schema. All 158 ported tests pass, including the broken fixture's 15 errors at the same lines.
- **System fonts and icons** instead of bundled web fonts: Segoe UI Variable (UI and the default paper font), Cambria (serif paper font), Cascadia Mono (code), Segoe Fluent Icons. This is a deliberate change from HANDOFF section 3 (Inter, Source Serif 4, JetBrains Mono): native Windows apps use the system fonts.
- **Window**: `WindowChrome` keeps the custom Nord header while Windows still provides edge resizing, Aero Snap, Win+arrow keys, the system menu (title-bar right-click, Alt+Space) and the window shadow. The maximise button reports `HTMAXBUTTON` to Windows, so Windows 11 shows Snap Layouts on hover. Maximising fills the work area (not under the taskbar). Rounded corners on Windows 11 via DWM.
- **Window memory**: `GetWindowPlacement`/`SetWindowPlacement` (the normal size even when maximised), saved in `settings.json`. A placement that is no longer on any monitor (unplugged screen) is ignored.
- **Marquee resize**: the preview is a separate click-through, never-activated overlay window covering the monitor's work area, so it can extend past the window's edges without resizing live. The grip is a WPF `Thumb` (a native drag handle, visible to screen readers). Alt+F8, or Enter on the grip, gives the keyboard version.
- **Context menus**: WPF's `ContextMenu` (keyboard navigation, access keys, Esc and click-outside closing, kept on screen at edges), built when it opens from what was right-clicked. Inner regions win (a text box inside a question inside a lesson); text boxes inside a region get their edit actions merged with the region's. Actions that can't run right now are shown disabled (edit actions) or left out (question actions that don't apply). Destructive actions ask first with the standard Windows dialog; deleting a lesson moves it to the Recycle Bin.
- **Native dialogs**: the Windows Open dialog for importing a file, the Save dialog for exporting a backup.
- **Accessibility fixes found while testing with UI Automation**: single-answer choices are real radio buttons (screen readers announce "selected"); choice, filter and segmented controls react to their checked state, not only mouse clicks; glossary entries are list items with names; answer boxes are explicitly editable (WPF's `IsReadOnly` is inherited, so text boxes inside the read-only document were silently read-only).
- **Distribution**: a self-contained folder build with ReadyToRun (no .NET install needed), in an NSIS per-user installer (45 MB). It installs into `%LOCALAPPDATA%\Nordlys` and takes over the Tauri version's uninstall entry, so it upgrades that version in place. The uninstaller removes app files only, never the data folder. The old Tauri uninstaller is not run, because its optional "delete app data" step would delete the progress folder.
- **Measured** (release builds, 5 runs, medians, `scripts/measure-desktop.ps1`): window shown after 674 ms (Tauri: 160 ms, but its page loaded later in the WebView); private memory 77 MB (Tauri + WebView2: 134 MB); 1 process (Tauri: 7); idle CPU 0 ms per 10 s (Tauri: 31–62). Single-file builds were slower to start (975–1104 ms, they unpack first) and used more memory, so the installer ships a folder.
- **Not verified with a real mouse** (no desktop-control tools in the build session): marquee dragging, Snap Layouts hover, drag-and-drop of order items and files. Everything else was driven through UI Automation, the Menu key and posted window messages on a throwaway data folder.

## Button interaction (0.2.1)
A light pass "as if macOS and an Arch Linux rice were combined":
- **Hover**: a soft tint fades over the control (100 ms in, 150 ms out) instead of a border change. Instant when Reduce motion is on or Windows' animation effects are off (`UI/Motion.cs`, an inherited flag the templates read).
- **Press**: a stronger tint and the control sinks to 97 % (98.5 % for answer choices, 98 % for sidebar items), instantly, like macOS.
- **Cursor**: the arrow on buttons (macOS); the pointing hand only on link-style buttons.
- **Focus ring**: still 2 px nord8 (HANDOFF 9.5), now rounded to the control with a soft outer glow.
- **Sidebar**: the open screen gets a 3 px accent bar on its left edge, like a tiling-WM bar's focused-workspace marker.
- **Disabled**: 45 % opacity. Window caption buttons are unchanged (they behave like Windows' own).

## In-app updates (0.3.0)
Elliott asked for updates fetched from GitHub, without a new installer each time.
- **Source**: GitHub Releases on the public repo (no token needed). `.github/workflows/release.yml` makes a release whenever main has a `<Version>` with no release yet, so shipping is "bump and push".
- **This relaxes "no network calls"** for one thing only: `api.github.com` for the version check and GitHub's download hosts for the zip. Redirects are followed by hand and refused if they leave those hosts. Downloads must match the release's `.sha256` file. The zip is extracted with a path check and must contain `Nordlys.exe`. Automatic checks run once a day, stay silent when offline, and can be switched off in Settings.
- **Unsigned**: the checksum guards against damaged downloads, not against someone with write access to the repo. Code signing costs money, and isn't worth it for one user.
- **Applying**: the download is staged in `%LOCALAPPDATA%\Nordlys-update` (beside the install, never inside it). "Restart to update" starts the staged copy with `--apply-update`. That copy:
  - waits for the app to close
  - renames the install folder aside
  - copies the new files in, keeping `uninstall.exe`
  - starts the app again with `--updated`

  If copying fails, the old folder is moved back and the app starts with `--update-failed`, which shows a notice. The version in Settings → Apps is updated only if that uninstall entry points at this folder. The whole folder is swapped, not file by file, so a half-updated app can't happen.
- **The notice** after an update opens Home (where notices show), not the resumed screen.
- **The first install of 0.3.0 is manual**: 0.2.x has no updater.
- **Checked end to end on throwaway folders**: a published 0.3.0 "install" updated itself to a 0.3.1 build from the Settings screen. It kept `uninstall.exe`, removed the staging folder, reopened on Home with "Nordlys is updated to 0.3.1." and kept the `--data` folder across the restart.

## Steel-gray Polar Night (0.3.1)
The dark grays (nord0–nord3, the tooltip, code-block background and faint text) are moderately darker and less blue, closer to steel gray: `#262A30`, `#30353C`, `#3A4048`, `#4A515B` instead of Nord's `#2E3440`…`#4C566A`. The accent and Snow Storm colours are unchanged. This is also the first release shipped through the in-app updater.
