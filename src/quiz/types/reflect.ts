// Not auto-graded: write an answer, show the model answer, rate yourself (the card handles rating).
import type { ReflectQuestion } from '../../lesson/types';
import { h } from '../../ui/components';
import type { TypeModule } from './common';

export const reflect: TypeModule<ReflectQuestion, string> = {
  check: () => ({ correct: true }),

  render(_q, ctx) {
    const area = h('textarea', { rows: 5, 'aria-label': 'Your answer', placeholder: 'Write your answer here. Ctrl+Enter shows the model answer.' });
    area.addEventListener('input', () => ctx.changed());
    return {
      el: h('div', { class: 'q-input' }, area),
      value: () => area.value,
      lock: () => (area.readOnly = true),
      mark: () => {},
      clearMark: () => {},
      focus: () => area.focus(),
    };
  },

  answerText: (q) => q.model_answer + (q.rubric.length ? `\n\n**Check your answer against these points:**\n\n${q.rubric.map((r) => `- ${r}`).join('\n')}` : ''),
};
