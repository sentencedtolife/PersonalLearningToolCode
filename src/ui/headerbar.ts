// GNOME/libadwaita-style title bar. The OS title bar is off (decorations: false).
import { getWindowControls } from '../platform';
import { h } from './components';
import { icon } from './icons';

let titleEl: HTMLElement;
let actionsEl: HTMLElement;
let backBtn: HTMLButtonElement;

export async function initHeaderbar(root: HTMLElement, onBack: () => void): Promise<void> {
  root.setAttribute('data-tauri-drag-region', '');
  backBtn = h('button', { class: 'icon-btn', type: 'button', title: 'Back (Esc)', 'aria-label': 'Back', html: icon('back'), onclick: onBack });
  const left = h('div', { class: 'hb-left', 'data-tauri-drag-region': true },
    backBtn,
    h('img', { class: 'hb-icon', src: '/icon.svg', alt: '' }));
  titleEl = h('div', { class: 'hb-title', 'data-tauri-drag-region': true, role: 'heading', 'aria-level': 1 }, 'Nordlys');
  actionsEl = h('div', { class: 'hb-actions' });
  const right = h('div', { class: 'hb-right', 'data-tauri-drag-region': true }, actionsEl);
  root.append(left, titleEl, right);

  const win = await getWindowControls();
  if (!win) return; // plain browser: no window controls
  const maxBtn = h('button', { class: 'win-btn', type: 'button', title: 'Maximise', 'aria-label': 'Maximise', html: icon('maximize'), onclick: () => win.toggleMaximize() });
  const syncMax = async () => {
    const max = await win.isMaximized();
    maxBtn.innerHTML = icon(max ? 'restore' : 'maximize');
    maxBtn.title = max ? 'Restore' : 'Maximise';
    maxBtn.setAttribute('aria-label', maxBtn.title);
  };
  win.onResized(() => void syncMax());
  void syncMax();
  right.append(h('div', { class: 'win-controls' },
    h('button', { class: 'win-btn', type: 'button', title: 'Minimise', 'aria-label': 'Minimise', html: icon('minimize'), onclick: () => win.minimize() }),
    maxBtn,
    h('button', { class: 'win-btn close', type: 'button', title: 'Close', 'aria-label': 'Close', html: icon('close'), onclick: () => win.close() })));
}

export function setHeader(title: string, actions: HTMLElement[] = [], showBack = true): void {
  titleEl.textContent = title;
  document.title = title === 'Nordlys' ? 'Nordlys' : `${title} · Nordlys`;
  actionsEl.replaceChildren(...actions);
  backBtn.classList.toggle('hidden', !showBack);
}
