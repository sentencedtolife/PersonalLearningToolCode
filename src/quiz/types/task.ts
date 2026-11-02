// An action outside the app. "I did this" checkbox; not scored, not reviewed.
import type { TaskQuestion } from '../../lesson/types';
import { highlightNow, renderMarkdown } from '../../lesson/render';
import { h } from '../../ui/components';
import type { TypeModule } from './common';

export const task: TypeModule<TaskQuestion, boolean> = {
  check: () => ({ correct: true }),

  render(q, ctx) {
    const box = h('input', { type: 'checkbox' });
    box.addEventListener('change', () => ctx.changed());
    const steps = h('div', { class: 'q-body', html: renderMarkdown(q.instructions) });
    highlightNow(steps);
    return {
      el: h('div', { class: 'q-input' },
        steps,
        h('div', { class: 'done-box' }, h('strong', {}, 'Done means'), q.done_means),
        h('label', { class: 'check' }, box, 'I did this')),
      value: () => box.checked,
      lock: () => {},
      mark: () => {},
      clearMark: () => {},
      focus: () => box.focus(),
    };
  },

  answerText: (q) => q.done_means,
};
