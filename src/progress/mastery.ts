// Unit mastery levels (HANDOFF 7.3). Never stored: always recomputed from history.
import type { Lesson } from '../lesson/types';
import { daysBetween, RESULT_SCORE, type Progress } from './progress';

export type Level = 0 | 1 | 2 | 3;
export const LEVEL_NAMES = ['Not started', 'Familiar', 'Proficient', 'Mastered'] as const;
export const LEVEL_RULES = [
  'Not started: no lesson in this unit practiced with a score of 70% or more yet.',
  'Familiar: at least 1 lesson in this unit practiced with a score of 70% or more.',
  'Proficient: 5 review answers in a row in this unit, each at least 2 days after you first saw that question, averaging 80% or more.',
  'Mastered: 8 review answers in a row in this unit, each at least 7 days after you first saw that question, averaging 90% or more.',
];

export const FAMILIAR_SCORE = 0.7;
const PROFICIENT = { gapDays: 2, count: 5, avg: 0.8 };
const MASTERED = { gapDays: 7, count: 8, avg: 0.9 };

interface Answer { date: string; gap: number; score: number }

/** True if some run of `n` consecutive answers averages at least `avg`. Once true, stays true as history grows. */
function someRunAverages(answers: Answer[], n: number, avg: number): boolean {
  if (answers.length < n) return false;
  let sum = 0;
  for (let i = 0; i < answers.length; i++) {
    sum += answers[i].score;
    if (i >= n) sum -= answers[i - n].score;
    if (i >= n - 1 && sum / n >= avg - 1e-9) return true;
  }
  return false;
}

export function unitLevel(unitId: string, lessons: Lesson[], p: Progress): Level {
  const inUnit = lessons.filter((l) => l.meta.unit === unitId);
  const answers: Answer[] = [];
  for (const l of inUnit) {
    for (const q of l.questions) {
      const qp = p.questions[`${l.meta.id}#${q.id}`];
      if (!qp) continue;
      for (const h of qp.history) answers.push({ date: h.date, gap: daysBetween(qp.firstSeen, h.date), score: RESULT_SCORE[h.result] });
    }
  }
  answers.sort((a, b) => (a.date < b.date ? -1 : a.date > b.date ? 1 : 0));

  if (someRunAverages(answers.filter((a) => a.gap >= MASTERED.gapDays), MASTERED.count, MASTERED.avg)) return 3;
  if (someRunAverages(answers.filter((a) => a.gap >= PROFICIENT.gapDays), PROFICIENT.count, PROFICIENT.avg)) return 2;
  const familiar = inUnit.some((l) => (p.lessons[l.meta.id]?.attempts ?? []).some((a) => a.score >= FAMILIAR_SCORE));
  return familiar ? 1 : 0;
}

export function allLevels(unitIds: string[], lessons: Lesson[], p: Progress): Map<string, Level> {
  return new Map(unitIds.map((id) => [id, unitLevel(id, lessons, p)]));
}
