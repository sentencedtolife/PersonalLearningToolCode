import { describe, expect, it } from 'vitest';
import type { Lesson, Question } from '../src/lesson/types';
import { buildContext, weakTags } from '../src/progress/context';
import { boxAfterFirstPractice, boxAfterReview, interval, recordAnswer, reviewQueue } from '../src/progress/leitner';
import { unitLevel } from '../src/progress/mastery';
import { pickNext } from '../src/progress/next';
import { addDays, daysBetween, emptyProgress, loadProgress, saveProgress, today, type Progress, type Result } from '../src/progress/progress';
import { BrowserStore } from '../src/store/browser-store';
import { parseCurriculum } from '../src/curriculum';
import { readRepoFile } from './helpers';

class MemoryStorage implements Storage {
  private m = new Map<string, string>();
  get length() { return this.m.size; }
  clear() { this.m.clear(); }
  getItem(k: string) { return this.m.get(k) ?? null; }
  key(i: number) { return [...this.m.keys()][i] ?? null; }
  removeItem(k: string) { this.m.delete(k); }
  setItem(k: string, v: string) { this.m.set(k, v); }
}

function q(id: string, type: Question['type'] = 'text', tags: string[] = []): Question {
  const base = { id, prompt: 'x', explanation: 'x', hints: [], tags, inPractice: true, line: 1 };
  if (type === 'task') return { ...base, type, instructions: 'x', done_means: 'x' };
  return { ...base, type: 'text', accept: ['x'], case_sensitive: false };
}

function lesson(id: string, o: Partial<Lesson['meta']> = {}, questions: Question[] = [q('q1'), q('q2')]): Lesson {
  return {
    meta: {
      id, title: id, track: 'C', unit: 'C.loops', tier: 1, energy: 2, minutes: 20, paper_type: 'concept',
      done_means: 'x', summary: 'x', prerequisites: [], tags: [], glossary: [], version: 1, ...o,
    },
    body: '',
    questions,
  };
}

describe('dates', () => {
  it('today, addDays, daysBetween', () => {
    expect(today(new Date(2026, 10, 2))).toBe('2026-11-02');
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-12-31', 16)).toBe('2027-01-16');
    expect(daysBetween('2026-11-02', '2026-11-09')).toBe(7);
    expect(daysBetween('2026-03-28', '2026-03-30')).toBe(2); // across a DST change
  });
});

describe('Leitner', () => {
  it('intervals are 1, 2, 4, 8, 16 days', () => {
    expect([1, 2, 3, 4, 5].map(interval)).toEqual([1, 2, 4, 8, 16]);
  });
  it('first practice: score 1 -> box 2, otherwise box 1', () => {
    expect(boxAfterFirstPractice(1)).toBe(2);
    expect(boxAfterFirstPractice(0.5)).toBe(1);
    expect(boxAfterFirstPractice(0)).toBe(1);
  });
  it('review: 1 -> up one (max 5), 0.5 -> same, 0 -> box 1', () => {
    expect(boxAfterReview(2, 1)).toBe(3);
    expect(boxAfterReview(5, 1)).toBe(5);
    expect(boxAfterReview(3, 0.5)).toBe(3);
    expect(boxAfterReview(4, 0)).toBe(1);
  });
  it('recordAnswer sets box, due and history', () => {
    const p = emptyProgress();
    const a = recordAnswer(p, 'l#q1', 'correct', '2026-11-02');
    expect(a).toMatchObject({ box: 2, due: '2026-11-04', firstSeen: '2026-11-02' });
    recordAnswer(p, 'l#q1', 'correct', '2026-11-04');
    expect(p.questions['l#q1']).toMatchObject({ box: 3, due: '2026-11-08', firstSeen: '2026-11-02' });
    recordAnswer(p, 'l#q1', 'self-partly', '2026-11-08');
    expect(p.questions['l#q1']).toMatchObject({ box: 3, due: '2026-11-12' });
    recordAnswer(p, 'l#q1', 'wrong', '2026-11-12');
    expect(p.questions['l#q1']).toMatchObject({ box: 1, due: '2026-11-13' });
    expect(p.questions['l#q1'].history.map((h) => h.result)).toEqual(['correct', 'correct', 'self-partly', 'wrong']);
  });
  it('review queue: due only, oldest first, max 15, no tasks, no missing lessons', () => {
    const l = lesson('l', {}, [...Array.from({ length: 20 }, (_, i) => q(`q${i}`)), q('t', 'task')]);
    const lessons = new Map([['l', l]]);
    const p = emptyProgress();
    for (let i = 0; i < 20; i++) p.questions[`l#q${i}`] = { box: 1, due: addDays('2026-11-01', -i), firstSeen: '2026-10-01', history: [] };
    p.questions['l#t'] = { box: 1, due: '2026-10-01', firstSeen: '2026-10-01', history: [] };
    p.questions['gone#q1'] = { box: 1, due: '2026-10-01', firstSeen: '2026-10-01', history: [] };
    p.questions['l#q0'].due = '2026-11-03'; // not due yet
    const queue = reviewQueue(p, lessons, '2026-11-02');
    expect(queue).toHaveLength(15);
    expect(queue[0]).toEqual({ lessonId: 'l', questionId: 'q19', due: '2026-10-13' });
    expect(queue.map((x) => x.due)).toEqual([...queue.map((x) => x.due)].sort());
    expect(queue.some((x) => x.questionId === 't' || x.lessonId === 'gone' || x.questionId === 'q0')).toBe(false);
  });
});

describe('mastery', () => {
  const l = lesson('l', {}, Array.from({ length: 8 }, (_, i) => q(`q${i}`)));
  const lessons = [l];

  function withHistory(entries: [string, string, Result][], firstSeen = '2026-11-01'): Progress {
    const p = emptyProgress();
    for (const [qid, date, result] of entries) {
      const key = `l#${qid}`;
      p.questions[key] ??= { box: 1, due: date, firstSeen, history: [] };
      p.questions[key].history.push({ date, result });
    }
    return p;
  }

  it('Not started without a practiced lesson scoring 70%+', () => {
    const p = emptyProgress();
    expect(unitLevel('C.loops', lessons, p)).toBe(0);
    p.lessons.l = { status: 'practiced', attempts: [{ date: '2026-11-01', score: 0.6, total: 5 }] };
    expect(unitLevel('C.loops', lessons, p)).toBe(0);
  });

  it('Familiar: a practice attempt of 0.7 or more', () => {
    const p = emptyProgress();
    p.lessons.l = { status: 'practiced', attempts: [{ date: '2026-11-01', score: 0.7, total: 5 }] };
    expect(unitLevel('C.loops', lessons, p)).toBe(1);
  });

  it('Proficient: 5 answers ≥2 days after first seen, averaging ≥0.8', () => {
    const late = (n: number, r: Result): [string, string, Result][] => Array.from({ length: n }, (_, i) => [`q${i}`, '2026-11-03', r]);
    expect(unitLevel('C.loops', lessons, withHistory(late(5, 'correct')))).toBe(2);
    expect(unitLevel('C.loops', lessons, withHistory(late(4, 'correct')))).toBe(0);
    // Same-day answers don't count.
    expect(unitLevel('C.loops', lessons, withHistory(Array.from({ length: 6 }, (_, i) => [`q${i}`, '2026-11-02', 'correct'] as [string, string, Result])))).toBe(0);
    // 4 correct + 1 half = 0.9 average: still proficient. 3 correct + 2 wrong = 0.6: not.
    expect(unitLevel('C.loops', lessons, withHistory([...late(4, 'correct'), ['q7', '2026-11-03', 'correct-hinted']]))).toBe(2);
    expect(unitLevel('C.loops', lessons, withHistory([...late(3, 'correct'), ['q6', '2026-11-03', 'wrong'], ['q7', '2026-11-03', 'wrong']]))).toBe(0);
  });

  it('Mastered: 8 answers ≥7 days after first seen, averaging ≥0.9', () => {
    const late = Array.from({ length: 8 }, (_, i) => [`q${i}`, '2026-11-08', 'correct'] as [string, string, Result]);
    expect(unitLevel('C.loops', lessons, withHistory(late))).toBe(3);
    const tooSoon = late.map(([id, , r]) => [id, '2026-11-07', r] as [string, string, Result]);
    expect(unitLevel('C.loops', lessons, withHistory(tooSoon))).toBe(2);
  });

  it('levels only go up: later bad answers do not remove Proficient', () => {
    const good = Array.from({ length: 5 }, (_, i) => [`q${i}`, '2026-11-03', 'correct'] as [string, string, Result]);
    const bad = Array.from({ length: 10 }, (_, i) => [`q${i % 8}`, '2026-11-05', 'wrong'] as [string, string, Result]);
    expect(unitLevel('C.loops', lessons, withHistory([...good, ...bad]))).toBe(2);
  });

  it('only counts lessons in that unit', () => {
    const late = Array.from({ length: 5 }, (_, i) => [`q${i}`, '2026-11-03', 'correct'] as [string, string, Result]);
    expect(unitLevel('C.verbs', lessons, withHistory(late))).toBe(0);
  });
});

describe('next lesson', () => {
  const levels = new Map<string, 0 | 1 | 2 | 3>([['C.verbs', 0], ['C.loops', 0], ['P.tour', 1]]);

  it('resumes the most recently opened lesson in progress, whatever the energy', () => {
    const lessons = new Map([['a', lesson('a', { energy: 3 })], ['b', lesson('b', { energy: 3 })], ['c', lesson('c', { energy: 1 })]]);
    const p = emptyProgress();
    p.lessons.a = { status: 'read', attempts: [], lastOpened: '2026-11-01T10:00:00Z' };
    p.lessons.b = { status: 'read', attempts: [], lastOpened: '2026-11-02T10:00:00Z' };
    expect(pickNext(lessons, p, 1, levels)).toEqual({ kind: 'resume', lessonId: 'b' });
  });

  it('only offers lessons at or below the chosen energy', () => {
    const lessons = new Map([['a', lesson('a', { energy: 3 })], ['b', lesson('b', { energy: 2 })]]);
    expect(pickNext(lessons, emptyProgress(), 2, levels)).toEqual({ kind: 'new', lessonId: 'b' });
    expect(pickNext(lessons, emptyProgress(), 1, levels)).toEqual({ kind: 'none' });
  });

  it('prefers met prerequisites, then lower tier, then oldest imported', () => {
    const lessons = new Map([
      ['unmet', lesson('unmet', { tier: 0, prerequisites: ['C.verbs'] })],
      ['met-t2', lesson('met-t2', { tier: 2, prerequisites: ['P.tour'] })],
      ['met-t1-new', lesson('met-t1-new', { tier: 1 })],
      ['met-t1-old', lesson('met-t1-old', { tier: 1 })],
    ]);
    const p = emptyProgress();
    p.lessons['met-t1-new'] = { status: 'new', attempts: [], imported: '2026-11-02T00:00:00Z' };
    p.lessons['met-t1-old'] = { status: 'new', attempts: [], imported: '2026-11-01T00:00:00Z' };
    expect(pickNext(lessons, p, 3, levels)).toEqual({ kind: 'new', lessonId: 'met-t1-old' });
    p.lessons['met-t1-old'].status = 'practiced';
    p.lessons['met-t1-new'].status = 'practiced';
    expect(pickNext(lessons, p, 3, levels)).toEqual({ kind: 'new', lessonId: 'met-t2' });
    p.lessons['met-t2'] = { status: 'practiced', attempts: [] };
    expect(pickNext(lessons, p, 3, levels)).toEqual({ kind: 'new', lessonId: 'unmet' });
  });

  it('a lesson-id prerequisite is met when that lesson is practiced', () => {
    const lessons = new Map([['first', lesson('first', { tier: 3 })], ['second', lesson('second', { tier: 0, prerequisites: ['first'] })]]);
    const p = emptyProgress();
    expect(pickNext(lessons, p, 3, levels)).toEqual({ kind: 'new', lessonId: 'first' });
    p.lessons.first = { status: 'practiced', attempts: [] };
    expect(pickNext(lessons, p, 3, levels)).toEqual({ kind: 'new', lessonId: 'second' });
  });
});

describe('learner context', () => {
  it('weak tags need 3+ answers averaging under 0.6', () => {
    const l = lesson('l', { tags: ['loops'] }, [q('q1', 'text', ['vectors']), q('q2', 'text', ['delta-time'])]);
    const p = emptyProgress();
    p.questions['l#q1'] = { box: 1, due: '', firstSeen: '', history: [{ date: 'd', result: 'wrong' }, { date: 'd', result: 'wrong' }, { date: 'd', result: 'correct' }] };
    p.questions['l#q2'] = { box: 1, due: '', firstSeen: '', history: [{ date: 'd', result: 'wrong' }, { date: 'd', result: 'wrong' }] };
    // vectors: 3 answers, avg 0.33. loops (lesson tag): 5 answers, avg 0.2. delta-time: only 2 answers.
    expect(weakTags([l], p)).toEqual(['loops', 'vectors']);
  });

  it('has every field from the handoff', () => {
    const cur = parseCurriculum(readRepoFile('content/curriculum.json'));
    const p = emptyProgress();
    const l = lesson('c-core-loops-01', { seed: { theme: 'lighthouse', verb: 'push' } });
    p.lessons['c-core-loops-01'] = { status: 'practiced', attempts: [{ date: '2026-11-02', score: 0.83, total: 6 }, { date: '2026-11-03', score: 1, total: 6 }], imported: '2026-11-01' };
    p.glossary['core loop'] = { definition: 'x', firstLesson: 'c-core-loops-01' };
    const text = buildContext({ date: '2026-11-03', energy: 2, request: 'teach me signals in Godot', lessons: [l], progress: p, curriculum: cur, levels: new Map([['C.loops', 1]]) });
    expect(text).toContain('=== LEARNER CONTEXT ===\ndate: 2026-11-03\nenergy: 2\nrequest: "teach me signals in Godot"\nlessons_practiced: 1');
    expect(text).toContain('unit_mastery: C.loops=Familiar');
    expect(text).toContain('recent (last 5): c-core-loops-01 1.0 | c-core-loops-01 0.83');
    expect(text).toContain('weak_tags (avg < 0.6, min 3 answers): none');
    expect(text).toContain('known_terms: core loop');
    expect(text).toContain('existing_ids: c-core-loops-01');
    expect(text).toContain('used_seeds (last 10): lighthouse/push');
    expect(text).toContain('  P (Programming & Game Maths in Godot): P.tour Godot tour: editor, nodes, scenes; P.vars Variables and types;');
    expect(text.endsWith('=== END CONTEXT ===')).toBe(true);
    const next = buildContext({ date: '2026-11-03', energy: 1, request: '  ', lessons: [], progress: emptyProgress(), curriculum: cur, levels: new Map() });
    expect(next).toContain('request: next');
  });
});

describe('progress.json storage', () => {
  it('round-trips, makes one backup per day of the previous state, keeps 14', async () => {
    const store = new BrowserStore(new MemoryStorage());
    const p = emptyProgress();
    await saveProgress(store, p, '2026-11-01');
    expect(await store.list('backups')).toEqual([]); // nothing to back up yet
    p.sessions.push({ date: '2026-11-01', energy: 1, lessonId: 'x', minutes: 5 });
    await saveProgress(store, p, '2026-11-01');
    expect(await store.list('backups')).toEqual(['progress-2026-11-01.json']);
    for (let d = 2; d <= 20; d++) await saveProgress(store, p, `2026-11-${String(d).padStart(2, '0')}`);
    const backups = await store.list('backups');
    expect(backups).toHaveLength(14);
    expect(backups[0]).toBe('progress-2026-11-07.json');
    const loaded = await loadProgress(store);
    expect(loaded.notice).toBeNull();
    expect(loaded.progress.sessions).toHaveLength(1);
  });

  it('a corrupt progress.json loads the newest valid backup and says so', async () => {
    const store = new BrowserStore(new MemoryStorage());
    const good = emptyProgress();
    good.sessions.push({ date: '2026-11-01', energy: 1, lessonId: 'x', minutes: 5 });
    await store.writeText('backups/progress-2026-10-30.json', JSON.stringify(emptyProgress()));
    await store.writeText('backups/progress-2026-10-31.json', JSON.stringify(good));
    await store.writeText('backups/progress-2026-11-01.json', '{broken');
    await store.writeText('progress.json', '{"lessons": ');
    const r = await loadProgress(store, '2026-11-02');
    expect(r.progress.sessions).toHaveLength(1);
    expect(r.notice).toBe('progress.json was damaged, so Nordlys loaded the backup from 2026-10-31.');
    expect(await store.exists('backups/progress-damaged-2026-11-02.json')).toBe(true);
  });

  it('no file = empty progress, no notice', async () => {
    const r = await loadProgress(new BrowserStore(new MemoryStorage()));
    expect(r).toEqual({ progress: emptyProgress(), notice: null });
  });
});
