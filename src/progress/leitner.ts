// Leitner review scheduling (HANDOFF 7.2).
import type { Lesson } from '../lesson/types';
import { addDays, RESULT_SCORE, type Progress, type QuestionProgress, type Result } from './progress';

/** Days until the next review, for boxes 1–5. */
export const INTERVALS = [1, 2, 4, 8, 16];
export const REVIEW_LIMIT = 15;

export function interval(box: number): number {
  return INTERVALS[Math.min(Math.max(box, 1), 5) - 1];
}

export function boxAfterFirstPractice(score: number): number {
  return score === 1 ? 2 : 1;
}

export function boxAfterReview(box: number, score: number): number {
  if (score === 1) return Math.min(box + 1, 5);
  if (score === 0) return 1;
  return box;
}

/** Records one answer. The first answer ever uses the first-practice rule; later ones use the review rule. */
export function recordAnswer(p: Progress, key: string, result: Result, date: string): QuestionProgress {
  const score = RESULT_SCORE[result];
  const existing = p.questions[key];
  const box = existing ? boxAfterReview(existing.box, score) : boxAfterFirstPractice(score);
  const q: QuestionProgress = existing ?? { box, due: date, firstSeen: date, history: [] };
  q.box = box;
  q.due = addDays(date, interval(box));
  q.history.push({ date, result });
  p.questions[key] = q;
  return q;
}

export interface DueItem { lessonId: string; questionId: string; due: string }

export function splitKey(key: string): [string, string] {
  const i = key.indexOf('#');
  return [key.slice(0, i), key.slice(i + 1)];
}

/** Due questions, oldest due date first. Skips tasks and questions whose lesson or id no longer exists. */
export function reviewQueue(p: Progress, lessons: Map<string, Lesson>, date: string, limit = REVIEW_LIMIT): DueItem[] {
  const items: DueItem[] = [];
  for (const [key, q] of Object.entries(p.questions)) {
    if (q.due > date) continue;
    const [lessonId, questionId] = splitKey(key);
    const question = lessons.get(lessonId)?.questions.find((x) => x.id === questionId);
    if (!question || question.type === 'task') continue;
    items.push({ lessonId, questionId, due: q.due });
  }
  items.sort((a, b) => (a.due < b.due ? -1 : a.due > b.due ? 1 : 0));
  return items.slice(0, limit);
}
