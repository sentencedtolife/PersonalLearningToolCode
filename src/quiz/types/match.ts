import type { MatchQuestion } from '../../lesson/types';
import { renderInline } from '../../lesson/render';
import { h } from '../../ui/components';
import { shuffled, type TypeModule } from './common';

let uid = 0;

/** Answer: for each left item (in order), the right item chosen for it. */
export const match: TypeModule<MatchQuestion, string[]> = {
  check: (q, answer) => ({ correct: q.pairs.every(([, right], i) => answer[i] === right) }),

  render(q, ctx) {
    const rights = shuffled(q.pairs.map((p) => p[1]), true);
    const base = `match-${++uid}`;
    const selects = q.pairs.map(([left], i) => {
      const sel = h('select', { 'aria-label': `Match for ${left}`, id: `${base}-${i}` },
        h('option', { value: '' }, 'Choose…'),
        rights.map((r) => h('option', { value: r }, r)));
      sel.addEventListener('change', () => ctx.changed());
      return sel;
    });
    const rows = q.pairs.map(([left], i) =>
      h('div', { class: 'match-row' }, h('label', { for: `${base}-${i}`, html: renderInline(left) }), selects[i]));
    return {
      el: h('div', { class: 'q-input' }, ...rows),
      value: () => (selects.every((s) => s.value) ? selects.map((s) => s.value) : null),
      lock: () => selects.forEach((s) => (s.disabled = true)),
      mark(s) {
        selects.forEach((sel, i) => {
          if (s === 'reveal') sel.value = q.pairs[i][1];
          sel.style.borderColor = s === 'not-yet' && sel.value !== q.pairs[i][1] ? 'var(--not-yet)' : 'var(--correct)';
        });
      },
      clearMark: () => selects.forEach((s) => (s.style.borderColor = '')),
      focus: () => selects[0]?.focus(),
    };
  },

  answerText: (q) => q.pairs.map(([l, r]) => `- ${l} → ${r}`).join('\n'),
};
