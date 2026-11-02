import { letterIndex, type MultiQuestion } from '../../lesson/types';
import { choiceList } from './choices';
import type { TypeModule } from './common';

export const multi: TypeModule<MultiQuestion, string[]> = {
  check(q, answer) {
    const want = [...new Set(q.answer.map((a) => a.toUpperCase()))].sort();
    const got = [...new Set(answer.map((a) => a.toUpperCase()))].sort();
    return { correct: want.length === got.length && want.every((a, i) => a === got[i]) };
  },

  render(q, ctx) {
    const list = choiceList(q.choices, true, ctx.changed);
    const hint = document.createElement('p');
    hint.className = 'small muted';
    hint.textContent = 'Select every correct choice.';
    list.el.prepend(hint);
    return {
      el: list.el,
      value: () => (list.selected().length ? list.selected() : null),
      lock: list.lock,
      mark: (s) => list.mark(s, q.answer),
      clearMark: list.clearMark,
      focus: list.focus,
      onKey: list.onKey,
    };
  },

  answerText: (q) => [...q.answer].sort().map((l) => `- **${l}.** ${q.choices[letterIndex(l)]}`).join('\n'),
};
