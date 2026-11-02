// Shared app state, loaded once at boot: store, settings, curriculum, lesson library, progress.
import { allUnits, parseCurriculum, type Curriculum } from './curriculum';
import type { Issue, Lesson } from './lesson/types';
import { loadLesson } from './lesson/validate';
import { allLevels, type Level } from './progress/mastery';
import { emptyProgress, lessonProgress, loadProgress, saveProgress as writeProgress, type Progress } from './progress/progress';
import { applySettings, parseSettings, type Settings } from './settings';
import { createStore, type Store } from './store/store';

/** Files in content/, keyed by their path inside the data folder (e.g. "lessons/x.lesson.md"). */
export const BUNDLED: Record<string, string> = Object.fromEntries(
  Object.entries(import.meta.glob('/content/**/*.{md,json}', { query: '?raw', import: 'default', eager: true }) as Record<string, string>)
    .map(([k, v]) => [k.replace(/^\/content\//, ''), v]),
);

/** Order the bundled lessons count as "imported", so Next follows the starting spine. */
const BUNDLED_ORDER = ['x-next-action-01', 'p-godot-tour-01', 'c-core-loops-01'];

export interface BrokenLesson { file: string; errors: Issue[] }

export const app = {
  store: null as unknown as Store,
  settings: null as unknown as Settings,
  curriculum: { version: 1, tracks: [] } as Curriculum,
  lessons: new Map<string, Lesson>(),
  broken: [] as BrokenLesson[],
  progress: emptyProgress() as Progress,
  /** One-line notice for Home (e.g. progress was restored from a backup). */
  notice: null as string | null,
  /** Energy picked on Home in this session. */
  energy: null as 1 | 2 | 3 | null,
  /** When each lesson was opened in this session (ms), for session minutes. */
  openedAt: new Map<string, number>(),
};

async function readOrNull(path: string): Promise<string | null> {
  try {
    return (await app.store.exists(path)) ? await app.store.readText(path) : null;
  } catch {
    return null;
  }
}

export async function loadApp(): Promise<void> {
  app.store = await createStore();
  const settingsText = await readOrNull('settings.json');
  app.settings = parseSettings(settingsText);
  applySettings(app.settings);

  // First run: copy bundled content into the data folder. Never overwrite on later runs.
  const firstRun = settingsText === null;
  if (firstRun) {
    for (const [path, text] of Object.entries(BUNDLED)) {
      if (!(await app.store.exists(path))) await app.store.writeText(path, text);
    }
    await saveSettings();
  }

  try {
    app.curriculum = parseCurriculum((await readOrNull('curriculum.json')) ?? BUNDLED['curriculum.json']);
  } catch {
    app.curriculum = parseCurriculum(BUNDLED['curriculum.json']);
    app.notice = 'curriculum.json could not be read, so Nordlys is using the built-in curriculum.';
  }

  const loaded = await loadProgress(app.store);
  app.progress = loaded.progress;
  app.notice = loaded.notice ?? app.notice;

  await reloadLessons();
  if (firstRun) {
    const base = Date.now();
    BUNDLED_ORDER.forEach((id, i) => {
      if (app.lessons.has(id)) lessonProgress(app.progress, id).imported = new Date(base + i).toISOString();
    });
    await saveProgress();
  }
}

export function knownUnits(): Set<string> {
  return new Set(allUnits(app.curriculum).map((u) => u.id));
}

export async function reloadLessons(): Promise<void> {
  const files = (await app.store.list('lessons')).filter((f) => f.endsWith('.lesson.md'));
  const units = knownUnits();
  app.lessons = new Map();
  app.broken = [];
  let changed = false;
  // Read all files at once: in the desktop app each read is a round trip to the Rust side.
  const texts = await Promise.all(files.sort().map((f) => app.store.readText(`lessons/${f}`).catch(() => '')));
  for (const [i, file] of files.entries()) {
    const r = loadLesson(texts[i], { knownUnits: units });
    if (!r.lesson) { app.broken.push({ file, errors: r.errors }); continue; }
    app.lessons.set(r.lesson.meta.id, r.lesson);
    const lp = lessonProgress(app.progress, r.lesson.meta.id);
    if (!lp.imported) { lp.imported = new Date().toISOString(); changed = true; }
  }
  if (changed) await saveProgress();
}

export async function saveLessonFile(id: string, text: string): Promise<void> {
  await app.store.writeText(`lessons/${id}.lesson.md`, text);
  await reloadLessons();
}

export async function readGeneratorPrompt(): Promise<string> {
  return (await readOrNull('generator-prompt.md')) ?? BUNDLED['generator-prompt.md'];
}

export async function resetGeneratorPrompt(): Promise<void> {
  await app.store.writeText('generator-prompt.md', BUNDLED['generator-prompt.md']);
}

export function levels(): Map<string, Level> {
  return allLevels(allUnits(app.curriculum).map((u) => u.id), [...app.lessons.values()], app.progress);
}

let saving: Promise<void> | null = null;
let dirty = false;

/**
 * Two writes never overlap, and a burst of calls (e.g. open lesson + record the route) becomes one write.
 * The returned promise resolves once the latest state is on disk.
 */
export function saveProgress(): Promise<void> {
  dirty = true;
  saving ??= (async () => {
    try {
      while (dirty) {
        await Promise.resolve(); // let calls made in the same tick join this write
        dirty = false;
        await writeProgress(app.store, app.progress);
      }
    } catch (err) {
      console.error('Saving progress failed', err);
    } finally {
      saving = null;
    }
  })();
  return saving;
}

export async function saveSettings(patch: Partial<Settings> = {}): Promise<void> {
  Object.assign(app.settings, patch);
  applySettings(app.settings);
  await app.store.writeText('settings.json', JSON.stringify(app.settings, null, 2));
}
