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
