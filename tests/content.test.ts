// The bundled content must follow the same rules Claude is given in generator-prompt.md.
import { describe, expect, it } from 'vitest';
import { loadLesson } from '../src/lesson/validate';
import { QUESTION_TYPES } from '../src/lesson/types';
import { bundledLessonFiles, knownUnits, readRepoFile } from './helpers';

const units = knownUnits();
const files = bundledLessonFiles();
const loaded = files.map((f) => ({ file: f, ...loadLesson(readRepoFile(`content/lessons/${f}`), { knownUnits: units }) }));

const SIZING: Record<number, { words: [number, number]; questions: [number, number] }> = {
  1: { words: [250, 450], questions: [3, 4] },
  2: { words: [700, 1200], questions: [6, 8] },
  3: { words: [1500, 2500], questions: [9, 12] },
};
const BANNED = /\b(simply|just|obviously|easy|explore|play around|get familiar with)\b|\betc\./i;

function paperWords(body: string): number {
  return body.replace(/<!--Q:[^>]*-->/g, ' ').split(/\s+/).filter((w) => /[A-Za-z0-9]/.test(w)).length;
}

describe('bundled lessons', () => {
  it('are the three from the handoff', () => {
    expect(files.sort()).toEqual(['c-core-loops-01.lesson.md', 'p-godot-tour-01.lesson.md', 'x-next-action-01.lesson.md']);
  });

  it.each(loaded.map((l) => [l.file, l] as const))('%s has 0 errors and 0 warnings', (_f, l) => {
    expect(l.errors).toEqual([]);
    expect(l.warnings).toEqual([]);
    expect(`${l.lesson!.meta.id}.lesson.md`).toBe(l.file);
  });

  it.each(loaded.map((l) => [l.file, l] as const))('%s follows the sizing and question-mix rules', (_f, l) => {
    const { meta, questions, body } = l.lesson!;
    const size = SIZING[meta.energy];
    const words = paperWords(body);
    expect(words, 'paper word count').toBeGreaterThanOrEqual(size.words[0]);
    expect(words, 'paper word count').toBeLessThanOrEqual(size.words[1]);
    expect(questions.length).toBeGreaterThanOrEqual(size.questions[0]);
    expect(questions.length).toBeLessThanOrEqual(size.questions[1]);
    expect(new Set(questions.map((q) => q.type)).size).toBeGreaterThanOrEqual(meta.energy === 1 ? 2 : 3);
    const inline = questions.filter((q) => !q.inPractice).length;
    expect(inline).toBeGreaterThanOrEqual(1);
    expect(inline).toBeLessThanOrEqual(2);
    if (meta.track === 'C' || meta.track === 'X') expect(questions.some((q) => q.type === 'reflect')).toBe(true);
    if (meta.track === 'P') expect(questions.some((q) => q.type === 'fill' || q.type === 'order')).toBe(true);
    for (const q of questions) expect(q.hints.length).toBeLessThanOrEqual(3);
  });

  it.each(loaded.map((l) => [l.file, l] as const))('%s uses no banned words', (file) => {
    const lines = readRepoFile(`content/lessons/${file}`).split(/\r?\n/);
    const hits = lines.map((t, i) => [i + 1, t] as const).filter(([, t]) => BANNED.test(t));
    expect(hits).toEqual([]);
  });

  it('use every question type at least once across the three', () => {
    const used = new Set(loaded.flatMap((l) => l.lesson!.questions.map((q) => q.type)));
    expect([...used].sort()).toEqual([...QUESTION_TYPES].sort());
  });

  it('p-godot-tour-01 includes a task', () => {
    const p = loaded.find((l) => l.file.startsWith('p-godot-tour'))!;
    expect(p.lesson!.questions.some((q) => q.type === 'task')).toBe(true);
  });
});

describe('generator prompt', () => {
  const prompt = readRepoFile('content/generator-prompt.md').replace(/\r\n/g, '\n');

  it('matches the text in HANDOFF.md exactly', () => {
    const handoff = readRepoFile('HANDOFF.md').replace(/\r\n/g, '\n');
    const start = handoff.indexOf('<<<BEGIN FILE: content/generator-prompt.md>>>\n');
    const end = handoff.indexOf('<<<END FILE: content/generator-prompt.md>>>');
    const expected = handoff.slice(start + '<<<BEGIN FILE: content/generator-prompt.md>>>\n'.length, end);
    expect(prompt).toBe(expected);
  });

  it('its example lesson passes the validator', () => {
    const r = loadLesson(prompt.slice(prompt.indexOf('````lesson')), { knownUnits: units });
    expect(r.errors).toEqual([]);
    expect(r.warnings).toEqual([]);
    expect(r.lesson!.meta.id).toBe('p-signals-01');
  });
});

describe('curriculum', () => {
  const cur = JSON.parse(readRepoFile('content/curriculum.json'));
  it('has the 7 tracks plus capstones, shares summing to 1', () => {
    expect(cur.tracks.map((t: { code: string }) => t.code)).toEqual(['C', 'X', 'P', 'D', 'A', 'S', 'N', 'K']);
    const sum = cur.tracks.reduce((s: number, t: { share: number }) => s + t.share, 0);
    expect(sum).toBeCloseTo(1);
  });
  it('every prerequisite is a real unit', () => {
    for (const t of cur.tracks) for (const u of t.units) for (const p of u.prereqs) expect(units.has(p), `${u.id} -> ${p}`).toBe(true);
  });
});
