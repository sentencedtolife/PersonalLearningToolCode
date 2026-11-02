import type { FillQuestion } from '../../lesson/types';
import { highlightNow, renderMarkdown } from '../../lesson/render';
import { BLANK_PATTERN } from '../../lesson/validate';
import { h } from '../../ui/components';
import { matchesAny, type TypeModule } from './common';

// An identifier-like token survives Markdown and code highlighting in one piece.
const token = (i: number) => `ZZBLANK${i}ZZ`;
const TOKEN_RE = /ZZBLANK(\d+)ZZ/g;

export const fill: TypeModule<FillQuestion, string[]> = {
  check(q, answer) {
    return { correct: q.blanks.every((accepted, i) => matchesAny(answer[i] ?? '', accepted, q.case_sensitive)) };
  },

  render(q, ctx) {
    let n = 0;
    const src = q.prompt.replace(BLANK_PATTERN, () => token(n++));
    const body = h('div', { class: 'q-body fill-prompt', html: renderMarkdown(src) });
    highlightNow(body);

    const inputs: HTMLInputElement[] = [];
    const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
    const nodes: Text[] = [];
    while (walker.nextNode()) nodes.push(walker.currentNode as Text);
    for (const node of nodes) {
      if (!TOKEN_RE.test(node.data)) continue;
      TOKEN_RE.lastIndex = 0;
      const frag = document.createDocumentFragment();
      let last = 0;
      for (const m of node.data.matchAll(TOKEN_RE)) {
        frag.append(node.data.slice(last, m.index));
        const i = Number(m[1]);
        const input = h('input', { type: 'text', autocomplete: 'off', spellcheck: 'false', 'aria-label': `Blank ${i + 1}` });
        input.addEventListener('input', () => ctx.changed());
        inputs[i] = input;
        frag.append(input);
        last = m.index! + m[0].length;
      }
      frag.append(node.data.slice(last));
      node.replaceWith(frag);
    }

    const setBorder = (color: string) => inputs.forEach((inp) => (inp.style.borderColor = color));
    return {
      el: body,
      ownsPrompt: true,
      value: () => (inputs.some((i) => i.value.trim()) ? inputs.map((i) => i.value) : null),
      lock: () => inputs.forEach((i) => (i.disabled = true)),
      mark(s) {
        if (s === 'not-yet') {
          inputs.forEach((inp, i) => (inp.style.borderColor = matchesAny(inp.value, q.blanks[i], q.case_sensitive) ? 'var(--correct)' : 'var(--not-yet)'));
        } else setBorder('var(--correct)');
      },
      clearMark: () => setBorder(''),
      focus: () => inputs[0]?.focus(),
    };
  },

  answerText: (q) => q.blanks.map((b, i) => `${i + 1}. **${b[0]}**`).join('\n'),
};
