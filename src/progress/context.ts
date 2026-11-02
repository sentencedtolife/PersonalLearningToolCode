// Builds the "learner context" block that goes after the generator prompt (HANDOFF 8).
import type { Curriculum } from '../curriculum';
import type { Lesson } from '../lesson/types';
import { LEVEL_NAMES, type Level } from './mastery';
import { RESULT_SCORE, type Progress } from './progress';

export interface ContextInput {
  date: string;
  energy: number;
  /** Empty = "next in curriculum". */
  request: string;
  lessons: Lesson[];
  progress: Progress;
  curriculum: Curriculum;
  levels: Map<string, Level>;
}

const WEAK_AVG = 0.6;
const WEAK_MIN_ANSWERS = 3;

function fmtScore(s: number): string {
  return s.toFixed(2).replace(/0$/, '');
}

export function weakTags(lessons: Lesson[], p: Progress): string[] {
  const sums = new Map<string, { total: number; n: number }>();
  for (const l of lessons) {
    for (const q of l.questions) {
      const qp = p.questions[`${l.meta.id}#${q.id}`];
      if (!qp) continue;
      const tags = new Set([...l.meta.tags, ...q.tags]);
      for (const h of qp.history) {
        for (const t of tags) {
          const s = sums.get(t) ?? { total: 0, n: 0 };
          s.total += RESULT_SCORE[h.result];
          s.n += 1;
          sums.set(t, s);
        }
      }
    }
  }
  return [...sums.entries()].filter(([, s]) => s.n >= WEAK_MIN_ANSWERS && s.total / s.n < WEAK_AVG).map(([t]) => t).sort();
}

export function buildContext(c: ContextInput): string {
  const { progress: p } = c;
  const practiced = c.lessons.filter((l) => p.lessons[l.meta.id]?.status === 'practiced' || (p.lessons[l.meta.id]?.attempts.length ?? 0) > 0);
  const mastery = c.curriculum.tracks.flatMap((t) => t.units).filter((u) => (c.levels.get(u.id) ?? 0) > 0).map((u) => `${u.id}=${LEVEL_NAMES[c.levels.get(u.id)!]}`);
  const recent = Object.entries(p.lessons)
    .flatMap(([id, lp]) => lp.attempts.map((a, i) => ({ id, a, i })))
    .sort((x, y) => y.a.date.localeCompare(x.a.date) || y.i - x.i)
    .slice(0, 5)
    .map(({ id, a }) => `${id} ${fmtScore(a.score)}`);
  const weak = weakTags(c.lessons, p);
  const seeds = c.lessons
    .filter((l) => l.meta.seed && (l.meta.seed.theme || l.meta.seed.verb))
    .sort((a, b) => (p.lessons[b.meta.id]?.imported ?? '').localeCompare(p.lessons[a.meta.id]?.imported ?? ''))
    .slice(0, 10)
    .map((l) => `${l.meta.seed!.theme ?? '?'}/${l.meta.seed!.verb ?? '?'}`);
  const units = c.curriculum.tracks.map((t) => `  ${t.code} (${t.name}): ${t.units.map((u) => `${u.id} ${u.title}`).join('; ')}`);
  const request = c.request.trim();

  return [
    '=== LEARNER CONTEXT ===',
    `date: ${c.date}`,
    `energy: ${c.energy}`,
    `request: ${request ? JSON.stringify(request) : 'next'}`,
    `lessons_practiced: ${practiced.length}`,
    `unit_mastery: ${mastery.join(', ') || 'none yet'}`,
    `recent (last 5): ${recent.join(' | ') || 'none yet'}`,
    `weak_tags (avg < ${WEAK_AVG}, min ${WEAK_MIN_ANSWERS} answers): ${weak.join(', ') || 'none'}`,
    `known_terms: ${Object.keys(p.glossary).join(', ') || 'none yet'}`,
    `existing_ids: ${c.lessons.map((l) => l.meta.id).sort().join(', ') || 'none'}`,
    `used_seeds (last 10): ${seeds.join(', ') || 'none'}`,
    'curriculum_units:',
    ...units,
    '=== END CONTEXT ===',
  ].join('\n');
}
