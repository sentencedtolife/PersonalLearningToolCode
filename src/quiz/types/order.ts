import type { OrderQuestion } from '../../lesson/types';
import { renderInline } from '../../lesson/render';
import { h, iconButton } from '../../ui/components';
import { shuffled, type TypeModule } from './common';

export const order: TypeModule<OrderQuestion, string[]> = {
  check: (q, answer) => ({ correct: answer.length === q.items.length && answer.every((t, i) => t === q.items[i]) }),

  render(q, ctx) {
    let current = shuffled(q.items, true);
    let picked: number | null = null; // index of the item picked up with Space/click
    let locked = false;
    let dragFrom: number | null = null;
    const list = h('ol', { class: 'order-list', 'aria-label': 'Items to put in order' });
    const help = h('p', { class: 'small muted' }, 'Drag the items, or select one (Space or click) and move it with ↑ and ↓.');

    const move = (from: number, to: number) => {
      if (locked || to < 0 || to >= current.length || from === to) return;
      const next = [...current];
      const [item] = next.splice(from, 1);
      next.splice(to, 0, item);
      current = next;
      if (picked === from) picked = to;
      draw(to);
      ctx.changed();
    };

    const draw = (focusIndex?: number) => {
      list.replaceChildren(...current.map((text, i) => {
        const li = h('li', {
          class: 'order-item', tabindex: 0, draggable: String(!locked), 'aria-selected': String(picked === i),
          'aria-label': `${i + 1}. ${text}${picked === i ? ' (selected)' : ''}`,
        },
          h('span', { class: 'order-num' }, `${i + 1}.`),
          h('span', { class: 'order-text', html: renderInline(text) }),
          iconButton('up', 'Move up', () => move(i, i - 1)),
          iconButton('down', 'Move down', () => move(i, i + 1)));
        li.addEventListener('click', (e) => {
          if ((e.target as HTMLElement).closest('button') || locked) return;
          picked = picked === i ? null : i;
          draw(i);
        });
        li.addEventListener('dragstart', (e) => { dragFrom = i; li.classList.add('dragging'); e.dataTransfer?.setData('text/plain', String(i)); });
        li.addEventListener('dragend', () => li.classList.remove('dragging'));
        li.addEventListener('dragover', (e) => e.preventDefault());
        li.addEventListener('drop', (e) => { e.preventDefault(); if (dragFrom !== null) move(dragFrom, i); dragFrom = null; });
        li.querySelectorAll('button').forEach((b) => { (b as HTMLButtonElement).disabled = locked; b.tabIndex = -1; });
        return li;
      }));
      if (focusIndex !== undefined) (list.children[focusIndex] as HTMLElement | undefined)?.focus();
    };
    draw();

    const focusedIndex = () => [...list.children].indexOf(document.activeElement as Element);

    return {
      el: h('div', { class: 'q-input' }, help, list),
      value: () => current,
      lock: () => { locked = true; picked = null; draw(); },
      mark(s) {
        [...list.children].forEach((li, i) => {
          const el = li as HTMLElement;
          el.style.borderColor = s === 'not-yet' ? (current[i] === q.items[i] ? 'var(--correct)' : 'var(--not-yet)') : 'var(--correct)';
        });
        if (s === 'reveal') { current = [...q.items]; draw(); [...list.children].forEach((li) => ((li as HTMLElement).style.borderColor = 'var(--correct)')); }
      },
      clearMark: () => [...list.children].forEach((li) => ((li as HTMLElement).style.borderColor = '')),
      focus: () => (list.children[0] as HTMLElement | undefined)?.focus(),
      onKey(e) {
        const i = focusedIndex();
        if (i === -1 || locked) return false;
        if (e.key === ' ') { picked = picked === i ? null : i; draw(i); return true; }
        if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
          const to = e.key === 'ArrowUp' ? i - 1 : i + 1;
          if (picked === i) move(i, to);
          else (list.children[to] as HTMLElement | undefined)?.focus();
          return true;
        }
        return false;
      },
    };
  },

  answerText: (q) => q.items.map((t, i) => `${i + 1}. ${t}`).join('\n'),
};
