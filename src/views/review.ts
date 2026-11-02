// Review: up to 15 due questions from the Leitner queue, same UI as Practice.
import { app, saveProgress } from '../app';
import { recordAnswer, reviewQueue } from '../progress/leitner';
import { RESULT_SCORE, today, type RunResult } from '../progress/progress';
import { navigate, type ViewFactory } from '../router';
import { button, h } from '../ui/components';
import { questionRunner, type RunItem } from './practice';

export function dueCount(): number {
  return reviewQueue(app.progress, app.lessons, today(), Infinity).length;
}

function nextDueDate(): string | null {
  const dates = Object.values(app.progress.questions).map((q) => q.due).sort();
  return dates.find((d) => d > today()) ?? null;
}

export const reviewView: ViewFactory = () => {
  const queue = reviewQueue(app.progress, app.lessons, today());
  if (!queue.length) {
    const next = nextDueDate();
    return {
      title: 'Review',
      el: h('div', { class: 'page page-narrow stack center' },
        h('h1', {}, 'Nothing to review today'),
        h('p', { class: 'muted' }, next ? `The next questions come back on ${next}.` : 'Questions appear here after you practise a lesson.'),
        button('Back to Home', () => navigate('#/home'), { variant: 'primary', key: 'Enter' })),
    };
  }

  const items: RunItem[] = queue.map((d) => {
    const lesson = app.lessons.get(d.lessonId)!;
    return { lesson, question: lesson.questions.find((q) => q.id === d.questionId)!, key: `${d.lessonId}#${d.questionId}` };
  });
  const results: Record<string, RunResult> = {};
  const runner = questionRunner({
    items,
    results,
    meta: (it, i) => `Review ${i + 1} of ${items.length} · from "${it.lesson.meta.title}"`,
    onAnswer: (it, r) => {
      if (r !== 'task') recordAnswer(app.progress, it.key, r, today());
      void saveProgress();
    },
    summary: () => {
      const scores = Object.values(results).filter((r): r is Exclude<RunResult, 'task'> => r !== 'task').map((r) => RESULT_SCORE[r]);
      const right = scores.filter((s) => s === 1).length;
      return h('div', { class: 'q-card stack center' },
        h('h2', {}, 'Review finished'),
        h('p', {}, `${right} of ${scores.length} correct first time. Each question comes back later: sooner if it was hard, later if it was easy for you.`),
        button('Back to Home', () => navigate('#/home'), { variant: 'primary', key: 'Enter' }));
    },
  });
  return { title: 'Review', ...runner, el: h('div', { class: 'page' }, runner.el) };
};
