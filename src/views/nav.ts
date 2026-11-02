// Small helpers shared by Home, Library and Map. Kept apart from the lesson/review views so those
// screens (and the Markdown, KaTeX and highlight.js they need) load only when first opened.
import { app } from '../app';
import { reviewQueue } from '../progress/leitner';
import { today } from '../progress/progress';

/** A lesson reopens in the mode it was left in. */
export function lessonRoute(id: string): string {
  return app.progress.lessons[id]?.lastPosition?.view === 'practice' ? `#/lesson/${id}/practice` : `#/lesson/${id}`;
}

export function dueCount(): number {
  return reviewQueue(app.progress, app.lessons, today(), Infinity).length;
}
