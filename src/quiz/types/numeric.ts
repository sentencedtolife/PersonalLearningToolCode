import type { NumericQuestion } from '../../lesson/types';
import { h } from '../../ui/components';
import type { TypeModule } from './common';

/** Accepts "," or "." as the decimal separator, and ignores the unit if typed after the number. */
export function parseNumber(input: string, unit?: string): number | null {
  let s = input.trim();
  if (unit && s.toLowerCase().endsWith(unit.toLowerCase())) s = s.slice(0, -unit.length).trim();
  s = s.replace(',', '.');
  if (!/^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(s)) return null;
  return Number(s);
}

export const numeric: TypeModule<NumericQuestion, string> = {
  check(q, answer) {
    const n = parseNumber(answer, q.unit);
    if (n === null) return { correct: false, feedback: 'That is not a number. Type digits only, like 3.5' };
    return { correct: Math.abs(n - q.answer) <= q.tolerance + 1e-9 };
  },

  render(q, ctx) {
    const input = h('input', { type: 'text', inputmode: 'decimal', autocomplete: 'off', 'aria-label': 'Your answer' });
    input.addEventListener('input', () => ctx.changed());
    const el = h('div', { class: 'q-input' }, h('div', { class: 'numeric-row' }, input, q.unit ? h('span', { class: 'muted' }, q.unit) : null));
    return {
      el,
      value: () => (input.value.trim() ? input.value : null),
      lock: () => (input.disabled = true),
      mark: (s) => (input.style.borderColor = s === 'not-yet' ? 'var(--not-yet)' : 'var(--correct)'),
      clearMark: () => (input.style.borderColor = ''),
      focus: () => input.focus(),
    };
  },

  answerText: (q) => `**${q.answer}${q.unit ? ` ${q.unit}` : ''}**${q.tolerance ? ` (anything from ${q.answer - q.tolerance} to ${q.answer + q.tolerance} counts)` : ''}`,
};
