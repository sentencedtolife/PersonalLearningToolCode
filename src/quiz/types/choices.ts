// Shared choice list for mcq and multi. Display order is shuffled; each choice keeps its letter.
import { letter } from '../../lesson/types';
import { renderInline } from '../../lesson/render';
import { h } from '../../ui/components';
import { shuffled, type MarkState } from './common';

export interface ChoiceList {
  el: HTMLElement;
  selected(): string[];
  lock(): void;
  mark(state: MarkState, correct: string[]): void;
  clearMark(): void;
  focus(): void;
  onKey(e: KeyboardEvent): boolean;
}

export function choiceList(choices: string[], multiple: boolean, changed: () => void): ChoiceList {
  const order = shuffled(choices.map((_, i) => i));
  const selected = new Set<string>();
  const buttons = new Map<string, HTMLButtonElement>();
  const el = h('div', { class: 'q-input', role: multiple ? 'group' : 'radiogroup', 'aria-label': 'Choices' });

  const toggle = (l: string) => {
    if (multiple) {
      if (selected.has(l)) selected.delete(l);
      else selected.add(l);
    } else {
      selected.clear();
      selected.add(l);
    }
    for (const [k, b] of buttons) b.setAttribute('aria-checked', String(selected.has(k)));
    changed();
  };

  order.forEach((ci, pos) => {
    const l = letter(ci);
    const b = h('button', {
      class: 'choice', type: 'button', role: multiple ? 'checkbox' : 'radio', 'aria-checked': 'false',
      onclick: () => toggle(l),
    },
      h('span', { class: 'choice-letter' }, l),
      h('span', { class: 'choice-text', html: renderInline(choices[ci]) }),
      h('span', { class: 'spacer' }),
      pos < 6 ? h('span', { class: 'key', 'aria-hidden': 'true', title: `Press ${pos + 1}` }, String(pos + 1)) : null);
    buttons.set(l, b);
    el.append(b);
  });

  return {
    el,
    selected: () => [...selected].sort(),
    lock: () => buttons.forEach((b) => (b.disabled = true)),
    mark(state, correct) {
      for (const [l, b] of buttons) {
        b.classList.remove('is-correct', 'is-not-yet');
        const isSel = selected.has(l);
        const isRight = correct.includes(l);
        if (state === 'correct' && isSel) b.classList.add('is-correct');
        if (state === 'not-yet' && isSel) b.classList.add('is-not-yet');
        if (state === 'reveal') {
          if (isRight) b.classList.add('is-correct');
          else if (isSel) b.classList.add('is-not-yet');
        }
      }
    },
    clearMark: () => buttons.forEach((b) => b.classList.remove('is-correct', 'is-not-yet')),
    focus: () => [...buttons.values()][0]?.focus(),
    onKey(e) {
      const n = Number(e.key);
      if (!e.ctrlKey && !e.altKey && !e.metaKey && Number.isInteger(n) && n >= 1 && n <= Math.min(6, order.length)) {
        const b = buttons.get(letter(order[n - 1]))!;
        if (b.disabled) return false;
        toggle(letter(order[n - 1]));
        b.focus();
        return true;
      }
      return false;
    },
  };
}
