import type { TextQuestion } from '../../lesson/types';
import { h } from '../../ui/components';
import { matchesAny, type TypeModule } from './common';

export const text: TypeModule<TextQuestion, string> = {
  check: (q, answer) => ({ correct: matchesAny(answer, q.accept, q.case_sensitive) }),

  render(_q, ctx) {
    const input = h('input', { type: 'text', autocomplete: 'off', spellcheck: 'false', 'aria-label': 'Your answer' });
    input.style.width = '100%';
    input.addEventListener('input', () => ctx.changed());
    return {
      el: h('div', { class: 'q-input' }, input),
      value: () => (input.value.trim() ? input.value : null),
      lock: () => (input.disabled = true),
      mark: (s) => (input.style.borderColor = s === 'not-yet' ? 'var(--not-yet)' : 'var(--correct)'),
      clearMark: () => (input.style.borderColor = ''),
      focus: () => input.focus(),
    };
  },

  answerText: (q) => (q.accept.length === 1 ? `**${q.accept[0]}**` : `**${q.accept[0]}** (also accepted: ${q.accept.slice(1).join(', ')})`),
};
