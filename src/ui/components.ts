import { icon } from './icons';

type Attrs = Record<string, string | number | boolean | null | undefined | EventListener>;
type Child = Node | string | null | undefined | false;

/** Tiny hyperscript helper: h('button', { class: 'btn', onclick: fn }, 'Label'). */
export function h<K extends keyof HTMLElementTagNameMap>(tag: K, attrs: Attrs = {}, ...children: (Child | Child[])[]): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined || v === false) continue;
    if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v as EventListener);
    else if (k === 'html') el.innerHTML = String(v);
    else el.setAttribute(k, v === true ? '' : String(v));
  }
  for (const c of children.flat()) {
    if (c === null || c === undefined || c === false) continue;
    el.append(typeof c === 'string' ? document.createTextNode(c) : c);
  }
  return el;
}

export type ButtonVariant = 'primary' | 'secondary' | 'flat';

export function button(label: string, onClick: () => void, opts: { variant?: ButtonVariant; key?: string; big?: boolean; wide?: boolean; title?: string } = {}): HTMLButtonElement {
  const cls = ['btn'];
  if (opts.variant === 'primary') cls.push('btn-primary');
  if (opts.variant === 'flat') cls.push('btn-flat');
  if (opts.big) cls.push('btn-big');
  if (opts.wide) cls.push('btn-wide');
  const b = h('button', { class: cls.join(' '), type: 'button', title: opts.title, onclick: () => onClick() }, label);
  if (opts.key) b.append(h('span', { class: 'key', 'aria-hidden': 'true' }, opts.key));
  return b;
}

export function iconButton(name: string, label: string, onClick: () => void): HTMLButtonElement {
  return h('button', { class: 'icon-btn', type: 'button', title: label, 'aria-label': label, html: icon(name), onclick: () => onClick() });
}

export function card(...children: (Child | Child[])[]): HTMLDivElement {
  return h('div', { class: 'card' }, ...children);
}

export function chip(text: string, style?: string): HTMLSpanElement {
  return h('span', { class: 'chip', style }, text);
}

export function trackChip(code: string, name: string): HTMLSpanElement {
  return h('span', { class: 'chip chip-track', style: `background: var(--track-${code})` }, name);
}

export type DotState = 'pending' | '1' | 'half' | '0' | 'task';

export function progressDots(states: DotState[], current: number): HTMLDivElement {
  const labels: Record<DotState, string> = { pending: 'not answered', '1': 'correct', half: 'partly', '0': 'not yet', task: 'task' };
  return h('div', { class: 'dots', role: 'img', 'aria-label': `Question ${current + 1} of ${states.length}` },
    states.map((s, i) => h('span', {
      class: `dot${s === 'pending' ? '' : ` r-${s}`}${i === current ? ' current' : ''}`,
      title: `Question ${i + 1}: ${labels[s]}`,
    })));
}

/** Segmented toggle used in the header bar (e.g. Paper / Practice). */
export function segmented(options: { label: string; active: boolean; onClick: () => void }[]): HTMLDivElement {
  return h('div', { class: 'seg', role: 'group' },
    options.map((o) => h('button', { type: 'button', 'aria-pressed': String(o.active), onclick: () => o.onClick() }, o.label)));
}

export function energyPicker(selected: number | null, onPick: (n: 1 | 2 | 3) => void): HTMLDivElement {
  const descs: Record<number, string> = { 1: 'Low · 5–10 min', 2: 'Medium · ~25 min', 3: 'High · 45–90 min' };
  return h('div', { class: 'energy-picker', role: 'group', 'aria-label': 'Energy' },
    ([1, 2, 3] as const).map((n) => h('button', {
      class: 'energy-btn', type: 'button', 'aria-pressed': String(selected === n), onclick: () => onPick(n),
    }, h('span', { class: 'energy-num' }, String(n)), h('span', { class: 'energy-desc' }, descs[n]), h('span', { class: 'key' }, String(n)))));
}

export function escapeHtml(s: string): string {
  return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);
}
