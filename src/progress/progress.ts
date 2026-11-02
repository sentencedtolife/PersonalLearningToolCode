// progress.json: load (with fallback to backups), save atomically, one backup per day.
import type { Store } from '../store/store';

export type Result = 'correct' | 'correct-hinted' | 'wrong' | 'self-got' | 'self-partly' | 'self-not';

export const RESULT_SCORE: Record<Result, number> = {
  correct: 1,
  'correct-hinted': 0.5,
  wrong: 0,
  'self-got': 1,
  'self-partly': 0.5,
  'self-not': 0,
};

/** What happened to one question in a practice run. 'task' = a task question (not scored). */
export type RunResult = Result | 'task';

/** Where the lesson was left. scroll = 0–1 through the paper. */
export interface Position { view: 'paper' | 'practice'; scroll: number }

/** A practice run in progress, so it can resume after a restart. Keyed by question id. */
export interface PracticeRun { results: Record<string, RunResult>; finished: boolean }

export interface Attempt { date: string; score: number; total: number }

export interface LessonProgress {
  status: 'new' | 'read' | 'practiced';
  lastPosition?: Position;
  run?: PracticeRun;
  attempts: Attempt[];
  /** ISO time the lesson file was added. Used for "oldest imported first". */
  imported?: string;
  /** ISO time the lesson was last opened. Used to pick which lesson to continue. */
  lastOpened?: string;
}

export interface HistoryEntry { date: string; result: Result }

export interface QuestionProgress {
  box: number;
  due: string;
  firstSeen: string;
  history: HistoryEntry[];
}

export interface Session { date: string; energy: number; lessonId: string; minutes: number }

export interface Progress {
  version: 1;
  lessons: Record<string, LessonProgress>;
  /** Key: "<lessonId>#<questionId>". */
  questions: Record<string, QuestionProgress>;
  taskChecks: Record<string, string>;
  sessions: Session[];
  glossary: Record<string, { definition: string; firstLesson: string }>;
  resume: { route: string };
}

export function emptyProgress(): Progress {
  return { version: 1, lessons: {}, questions: {}, taskChecks: {}, sessions: [], glossary: {}, resume: { route: '#/home' } };
}

/** Returns null if the text is not a usable progress file. */
export function parseProgress(text: string): Progress | null {
  try {
    const raw = JSON.parse(text);
    if (!raw || typeof raw !== 'object' || typeof raw.lessons !== 'object' || typeof raw.questions !== 'object') return null;
    return { ...emptyProgress(), ...raw };
  } catch {
    return null;
  }
}

/** Local calendar date as YYYY-MM-DD. */
export function today(d: Date = new Date()): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function addDays(date: string, n: number): string {
  const [y, m, d] = date.split('-').map(Number);
  return today(new Date(y, m - 1, d + n));
}

export function daysBetween(from: string, to: string): number {
  const [y1, m1, d1] = from.split('-').map(Number);
  const [y2, m2, d2] = to.split('-').map(Number);
  return Math.round((Date.UTC(y2, m2 - 1, d2) - Date.UTC(y1, m1 - 1, d1)) / 86_400_000);
}

export function lessonProgress(p: Progress, id: string): LessonProgress {
  return (p.lessons[id] ??= { status: 'new', attempts: [] });
}

/** After a lesson is replaced: keep progress for question ids that still exist, drop the rest. */
export function pruneLessonQuestions(p: Progress, lessonId: string, keepIds: string[]): string[] {
  const keep = new Set(keepIds);
  const dropped: string[] = [];
  const prefix = `${lessonId}#`;
  for (const map of [p.questions, p.taskChecks] as Record<string, unknown>[]) {
    for (const key of Object.keys(map)) {
      if (key.startsWith(prefix) && !keep.has(key.slice(prefix.length))) {
        delete map[key];
        dropped.push(key);
      }
    }
  }
  const run = p.lessons[lessonId]?.run;
  if (run) for (const qid of Object.keys(run.results)) if (!keep.has(qid)) delete run.results[qid];
  return dropped;
}

const FILE = 'progress.json';
const BACKUP_DIR = 'backups';
const KEEP_BACKUPS = 14;

export async function loadProgress(store: Store, date = today()): Promise<{ progress: Progress; notice: string | null }> {
  if (!(await store.exists(FILE))) return { progress: emptyProgress(), notice: null };
  const text = await store.readText(FILE).catch(() => '');
  const parsed = parseProgress(text);
  if (parsed) return { progress: parsed, notice: null };

  // Damaged: keep a copy for inspection, then fall back to the newest valid backup.
  await store.writeText(`${BACKUP_DIR}/progress-damaged-${date}.json`, text).catch(() => {});
  const backups = (await store.list(BACKUP_DIR)).filter((f) => /^progress-\d{4}-\d{2}-\d{2}\.json$/.test(f)).sort().reverse();
  for (const f of backups) {
    const p = parseProgress(await store.readText(`${BACKUP_DIR}/${f}`).catch(() => ''));
    if (p) return { progress: p, notice: `progress.json was damaged, so Nordlys loaded the backup from ${f.slice(9, 19)}.` };
  }
  return { progress: emptyProgress(), notice: 'progress.json was damaged and no backup could be read, so progress starts empty.' };
}

export async function saveProgress(store: Store, progress: Progress, date = today()): Promise<void> {
  const backup = `${BACKUP_DIR}/progress-${date}.json`;
  if (!(await store.exists(backup)) && (await store.exists(FILE))) {
    // First save of the day: keep yesterday's end state.
    const previous = await store.readText(FILE);
    if (parseProgress(previous)) {
      await store.writeText(backup, previous);
      const all = (await store.list(BACKUP_DIR)).filter((f) => /^progress-\d{4}-\d{2}-\d{2}\.json$/.test(f)).sort();
      for (const old of all.slice(0, Math.max(0, all.length - KEEP_BACKUPS))) await store.remove(`${BACKUP_DIR}/${old}`);
    }
  }
  await store.writeText(FILE, JSON.stringify(progress, null, 1));
}
