import { letterIndex, type McqQuestion } from '../../lesson/types';
import { choiceList } from './choices';
import type { TypeModule } from './common';

export const mcq: TypeModule<McqQuestion, string> = {
  check(q, answer) {
    const correct = answer.toUpperCase() === q.answer;
    return { correct, feedback: correct ? undefined : q.feedback[answer.toUpperCase()] };
  },

  render(q, ctx) {
    const list = choiceList(q.choices, false, ctx.changed);
    return {
      el: list.el,
      value: () => list.selected()[0] ?? null,
      lock: list.lock,
      mark: (s) => list.mark(s, [q.answer]),
      clearMark: list.clearMark,
      focus: list.focus,
      onKey: list.onKey,
    };
  },

  answerText: (q) => `**${q.answer}.** ${q.choices[letterIndex(q.answer)]}`,
};
