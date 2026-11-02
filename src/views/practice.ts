// One question per screen with progress dots. Used by lesson Practice mode and by Review.
import { app, saveProgress } from '../app';
import type { Lesson, Question } from '../lesson/types';
import { recordAnswer } from '../progress/leitner';
import { lessonProgress, RESULT_SCORE, today, type RunResult } from '../progress/progress';
import { questionCard, type Card } from '../quiz/card';
import { scoreRun } from '../quiz/engine';
import { navigate, type View } from '../router';
import { button, h, progressDots, type DotState } from '../ui/components';
import { lessonHeader } from './lesson';

export interface RunItem { lesson: Lesson; question: Question; key: string }

export interface RunOptions {
  items: RunItem[];
  /** Results so far, keyed by item key. Items with a result are skipped when resuming. */
  results: Record<string, RunResult>;
  meta(item: RunItem, index: number): string;
  onAnswer(item: RunItem, result: RunResult): void;
  onTask?(item: RunItem, checked: boolean): void;
  /** Shown after the last question. */
  summary(): HTMLElement;
}

function dotState(r: RunResult | undefined): DotState {
  if (r === undefined) return 'pending';
  if (r === 'task') return 'task';
  const s = RESULT_SCORE[r];
  return s === 1 ? '1' : s === 0 ? '0' : 'half';
}

export function questionRunner(o: RunOptions): Pick<View, 'el' | 'onKey' | 'mounted'> {
  const el = h('div', { class: 'practice' });
  let card: Card | null = null;

  const show = (index: number) => {
    const dots = progressDots(o.items.map((it) => dotState(o.results[it.key])), Math.min(index, o.items.length - 1));
    if (index >= o.items.length) {
      card = null;
      el.replaceChildren(dots, o.summary());
      (el.querySelector('.btn-primary') as HTMLElement | null)?.focus();
      return;
    }
    const item = o.items[index];
    const last = index === o.items.length - 1;
    card = questionCard({
      question: item.question,
      meta: o.meta(item, index),
      nextLabel: last ? 'See summary' : 'Next question',
      onNext: () => show(index + 1),
      onDone: (r) => {
        o.results[item.key] = r;
        o.onAnswer(item, r);
        dots.replaceWith(progressDots(o.items.map((it) => dotState(o.results[it.key])), index));
      },
      onTaskCheck: (checked) => o.onTask?.(item, checked),
    });
    el.replaceChildren(dots, card.el);
    card.focus();
    document.getElementById('content')!.scrollTop = 0;
  };

  const first = o.items.findIndex((it) => o.results[it.key] === undefined);
  return {
    el,
    mounted: () => show(first === -1 ? o.items.length : first),
    onKey: (e) => card?.onKey(e) ?? false,
  };
}

/** Practice mode for one lesson: all questions in file order. Progress is saved after every answer. */
export function practiceMode(lesson: Lesson): Omit<View, 'title' | 'actions'> {
  const id = lesson.meta.id;
  const lp = lessonProgress(app.progress, id);
  if (!lp.run) lp.run = { results: {}, finished: false };
  const run = lp.run;
  const items: RunItem[] = lesson.questions.map((q) => ({ lesson, question: q, key: q.id }));
  const scored = items.filter((it) => it.question.type !== 'task').length;

  const summary = () => {
    const { points, total, score } = scoreRun(items.map((it) => run.results[it.key]).filter((r): r is RunResult => r !== undefined));
    if (!run.finished) {
      run.finished = true;
      lp.attempts.push({ date: today(), score: Math.round(score * 100) / 100, total });
      void saveProgress();
    }
    const markDone = () => {
      lp.status = 'practiced';
      delete lp.run;
      lp.lastPosition = undefined;
      const opened = app.openedAt.get(id);
      const minutes = opened ? Math.min(240, Math.max(1, Math.round((Date.now() - opened) / 60_000))) : lesson.meta.minutes;
      app.progress.sessions.push({ date: today(), energy: app.energy ?? lesson.meta.energy, lessonId: id, minutes });
      app.openedAt.delete(id);
      void saveProgress();
      navigate('#/home');
    };
    const again = () => {
      lp.run = { results: {}, finished: false };
      void saveProgress();
      navigate(`#/lesson/${id}/practice`);
    };
    return h('div', { class: 'q-card stack center' },
      h('h2', {}, 'Practice finished'),
      total ? h('div', { class: 'summary-score' }, `${Math.round(score * 100)}%`) : null,
      total ? h('p', { class: 'muted' }, `${points} out of ${total} point${total === 1 ? '' : 's'}. Correct first time = 1 point, with a hint or second try = ½ point.`) : null,
      h('div', { class: 'done-box', style: 'text-align: left' }, h('strong', {}, 'Done means'), lesson.meta.done_means),
      h('div', { class: 'row', style: 'justify-content: center' },
        button('Mark lesson done', markDone, { variant: 'primary', key: 'Enter' }),
        button('Practice again', again, { variant: 'flat' })));
  };

  const runner = questionRunner({
    items,
    results: run.results,
    meta: (_it, i) => `Question ${i + 1} of ${items.length}${scored < items.length && items[i].question.type === 'task' ? ' · task (not scored)' : ''}`,
    onAnswer: (it, r) => {
      if (r !== 'task') recordAnswer(app.progress, `${id}#${it.question.id}`, r, today());
      void saveProgress();
    },
    onTask: (it, checked) => {
      const key = `${id}#${it.question.id}`;
      if (checked) app.progress.taskChecks[key] = today();
      else delete app.progress.taskChecks[key];
      void saveProgress();
    },
    summary,
  });

  const header = lessonHeader(lesson);
  header.querySelector('.terms-row')?.remove();
  header.querySelector('.prereq-note')?.remove();
  return { ...runner, el: h('div', { class: 'page' }, header, runner.el) };
}
