// One question on screen: prompt, answer widget, Check / Hint / Show answer, feedback, Next.
import { highlightNow, renderMarkdown } from '../lesson/render';
import type { Question } from '../lesson/types';
import type { RunResult } from '../progress/progress';
import { button, h } from '../ui/components';
import { Attempt, type SelfRating } from './engine';
import { typeModule } from './types';

export interface CardOptions {
  question: Question;
  /** Small line above the prompt, e.g. "Question 3 of 7". */
  meta?: string;
  /** Label of the button shown when the question is finished. No button if absent. */
  nextLabel?: string;
  onNext?(): void;
  /** Fires once, when the question is finished. */
  onDone?(result: RunResult): void;
  onTaskCheck?(checked: boolean): void;
}

export interface Card {
  el: HTMLElement;
  attempt: Attempt;
  onKey(e: KeyboardEvent): boolean;
  focus(): void;
}

function md(src: string): HTMLElement {
  const div = h('div', { html: renderMarkdown(src) });
  highlightNow(div);
  return div;
}

function feedbackBox(kind: 'correct' | 'not-yet' | 'info', title: string, ...parts: (string | undefined)[]): HTMLElement {
  return h('div', { class: `feedback ${kind}` }, h('div', { class: 'feedback-title' }, title), ...parts.filter((p): p is string => !!p).map(md));
}

export function questionCard(o: CardOptions): Card {
  const q = o.question;
  const attempt = new Attempt(q);
  const mod = typeModule(q);
  let modelShown = false;

  const hintsEl = h('div');
  const feedbackEl = h('div', { 'aria-live': 'polite' });
  const actions = h('div', { class: 'q-actions' });

  const widget = mod.render(q, {
    submit: () => check(),
    changed: () => {
      if (q.type === 'task') o.onTaskCheck?.(widget.value() as boolean);
      else if (!attempt.done) { widget.clearMark(); if (feedbackEl.dataset.kind === 'nudge') feedbackEl.replaceChildren(); }
    },
  });

  const promptEl = widget.ownsPrompt ? null : h('div', { class: 'q-body' }, md(q.prompt));
  const el = h('div', { class: 'q-card' },
    o.meta ? h('div', { class: 'q-meta' }, o.meta) : null,
    promptEl,
    widget.el,
    hintsEl,
    feedbackEl,
    actions);

  const setFeedback = (box: HTMLElement | null, kind = '') => {
    feedbackEl.dataset.kind = kind;
    feedbackEl.replaceChildren(...(box ? [box] : []));
  };

  const next = () => {
    if (q.type === 'task' && !attempt.done) { attempt.completeTask(); o.onDone?.('task'); }
    o.onNext?.();
  };

  const finish = () => {
    widget.lock();
    o.onDone?.(attempt.result!);
    draw();
    (actions.querySelector('.btn-primary') as HTMLElement | null)?.focus();
  };

  const check = () => {
    if (attempt.done || q.type === 'reflect' || q.type === 'task') return;
    const value = widget.value();
    if (value === null) {
      setFeedback(h('p', { class: 'muted small' }, 'Choose or type an answer first.'), 'nudge');
      return;
    }
    const r = mod.check(q, value as never);
    const outcome = attempt.submit(r.correct);
    if (outcome === 'correct') {
      widget.mark('correct');
      setFeedback(feedbackBox('correct', 'Correct', q.explanation));
      finish();
    } else if (outcome === 'not-yet') {
      widget.mark('not-yet');
      const tail = attempt.hintsLeft > 0 ? 'Try again, or take a hint. You have 1 more try.' : 'Try again. You have 1 more try.';
      setFeedback(feedbackBox('not-yet', 'Not yet', r.feedback, tail));
      draw();
    } else {
      widget.mark('reveal');
      setFeedback(feedbackBox('info', 'Here is the answer', r.feedback, mod.answerText(q), q.explanation));
      finish();
    }
  };

  const showHint = () => {
    const hint = attempt.nextHint();
    if (hint === null) return;
    hintsEl.append(h('div', { class: 'hint' }, h('strong', {}, `Hint ${attempt.hintsShown}: `), md(hint)));
    draw();
  };

  const showAnswer = () => {
    if (attempt.done) return;
    attempt.reveal();
    widget.mark('reveal');
    setFeedback(feedbackBox('info', 'Here is the answer', mod.answerText(q), q.explanation));
    finish();
  };

  const showModel = () => {
    if (modelShown) return;
    modelShown = true;
    widget.lock();
    setFeedback(feedbackBox('info', 'Model answer', mod.answerText(q), q.explanation));
    draw();
    (actions.querySelector('button') as HTMLElement | null)?.focus();
  };

  const rate = (r: SelfRating) => {
    if (!modelShown || attempt.done) return;
    attempt.selfRate(r);
    finish();
  };

  const draw = () => {
    actions.replaceChildren();
    const nextBtn = () => (o.nextLabel ? button(o.nextLabel, next, { variant: 'primary', key: 'Enter' }) : null);
    if (q.type === 'task' || attempt.done) {
      const b = nextBtn();
      if (b) actions.append(b);
      if (attempt.done && q.type === 'reflect') {
        const labels: Record<string, string> = { 'self-got': 'Got it', 'self-partly': 'Partly', 'self-not': 'Not yet' };
        actions.prepend(h('span', { class: 'muted small' }, `You rated this: ${labels[attempt.result!]}`));
      }
      return;
    }
    if (q.type === 'reflect') {
      if (!modelShown) actions.append(button('Show model answer', showModel, { variant: 'primary', key: 'Ctrl+Enter' }));
      else actions.append(
        h('span', { class: 'muted small' }, 'How did your answer compare?'),
        h('div', { class: 'self-rate' },
          button('Got it', () => rate('got'), { key: '1' }),
          button('Partly', () => rate('partly'), { key: '2' }),
          button('Not yet', () => rate('not'), { key: '3' })));
      return;
    }
    actions.append(button('Check answer', check, { variant: 'primary', key: 'Enter' }));
    if (attempt.hintsLeft > 0) actions.append(button(`Hint (${attempt.hintsLeft} left)`, showHint, { key: 'H' }));
    actions.append(button('Show answer', showAnswer, { variant: 'flat' }));
  };
  draw();

  return {
    el,
    attempt,
    focus: () => widget.focus(),
    onKey(e) {
      const t = e.target as HTMLElement;
      const mods = e.ctrlKey || e.metaKey || e.altKey;
      if (e.key === 'Enter' && !e.shiftKey) {
        if (e.ctrlKey || e.metaKey) {
          if (q.type === 'reflect' && !modelShown) { showModel(); return true; }
          return false;
        }
        if (e.altKey || t.tagName === 'TEXTAREA') return false;
        if (t.tagName === 'BUTTON' && !t.classList.contains('choice')) return false; // let the focused button act
        if (q.type === 'task' || attempt.done) {
          if (o.onNext) { next(); return true; }
          return false;
        }
        if (q.type === 'reflect') return false;
        check();
        return true;
      }
      const typing = t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT';
      if (typing || mods) return false;
      if ((e.key === 'h' || e.key === 'H') && attempt.hintsLeft > 0 && q.type !== 'reflect') { showHint(); return true; }
      if (modelShown && !attempt.done && ['1', '2', '3'].includes(e.key)) {
        rate(e.key === '1' ? 'got' : e.key === '2' ? 'partly' : 'not');
        return true;
      }
      if (!attempt.done && widget.onKey?.(e)) return true;
      return false;
    },
  };
}
