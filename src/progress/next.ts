// Picking the next lesson from the library (HANDOFF 7.4).
import type { Lesson } from '../lesson/types';
import type { Level } from './mastery';
import type { Progress } from './progress';

export type NextPick =
  | { kind: 'resume'; lessonId: string }
  | { kind: 'new'; lessonId: string }
  | { kind: 'none' };

/** A prerequisite is met if it is a practiced lesson, or a unit at Familiar or above. Unknown ids are not met. */
export function prerequisitesMet(lesson: Lesson, lessons: Map<string, Lesson>, p: Progress, levels: Map<string, Level>): boolean {
  return lesson.meta.prerequisites.every((id) => {
    if (lessons.has(id)) return p.lessons[id]?.status === 'practiced';
    return (levels.get(id) ?? 0) >= 1;
  });
}

export function missingPrerequisites(lesson: Lesson, lessons: Map<string, Lesson>, levels: Map<string, Level>): string[] {
  return lesson.meta.prerequisites.filter((id) => !lessons.has(id) && !levels.has(id));
}

export function pickNext(lessons: Map<string, Lesson>, p: Progress, energy: number, levels: Map<string, Level>): NextPick {
  const all = [...lessons.values()];

  // 1. A lesson in progress: the most recently opened one.
  const inProgress = all
    .filter((l) => p.lessons[l.meta.id]?.status === 'read')
    .sort((a, b) => (p.lessons[b.meta.id]?.lastOpened ?? '').localeCompare(p.lessons[a.meta.id]?.lastOpened ?? ''));
  if (inProgress.length) return { kind: 'resume', lessonId: inProgress[0].meta.id };

  // 2. Unpracticed lessons that fit the energy: prerequisites met first, then lower tier, then oldest imported.
  const candidates = all
    .filter((l) => (p.lessons[l.meta.id]?.status ?? 'new') !== 'practiced' && l.meta.energy <= energy)
    .map((l) => ({ l, met: prerequisitesMet(l, lessons, p, levels), imported: p.lessons[l.meta.id]?.imported ?? '' }))
    .sort((a, b) => Number(b.met) - Number(a.met) || a.l.meta.tier - b.l.meta.tier || a.imported.localeCompare(b.imported) || a.l.meta.id.localeCompare(b.l.meta.id));
  if (candidates.length) return { kind: 'new', lessonId: candidates[0].l.meta.id };

  // 3. Nothing ready.
  return { kind: 'none' };
}
