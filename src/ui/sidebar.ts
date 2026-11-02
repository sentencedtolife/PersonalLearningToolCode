import { h } from './components';
import { icon } from './icons';

export const NAV = [
  { route: '#/home', label: 'Home', icon: 'home', key: 'G H' },
  { route: '#/library', label: 'Library', icon: 'library', key: 'G L' },
  { route: '#/map', label: 'Map', icon: 'map', key: 'G M' },
  { route: '#/review', label: 'Review', icon: 'review', key: 'G R' },
  { route: '#/ask', label: 'Ask', icon: 'ask', key: 'G A' },
  { route: '#/import', label: 'Import', icon: 'import', key: 'G I' },
  { route: '#/glossary', label: 'Glossary', icon: 'glossary', key: '' },
  { route: '#/settings', label: 'Settings', icon: 'settings', key: '' },
];

let items: HTMLAnchorElement[] = [];

export function initSidebar(root: HTMLElement, collapsed: boolean, onToggle: (collapsed: boolean) => void): void {
  root.setAttribute('aria-label', 'Main');
  items = NAV.map((n) => h('a', { class: 'nav-item', href: n.route, title: n.label },
    h('span', { html: icon(n.icon) }),
    h('span', { class: 'nav-label' }, n.label),
    n.key ? h('span', { class: 'nav-key', 'aria-hidden': 'true' }, n.key) : null));
  const toggle = h('button', { class: 'nav-item nav-collapse', type: 'button' },
    h('span', { html: icon('menu') }), h('span', { class: 'nav-label' }, 'Collapse sidebar'));
  const apply = (c: boolean) => {
    document.documentElement.dataset.sidebar = c ? 'collapsed' : 'open';
    toggle.setAttribute('aria-expanded', String(!c));
    toggle.title = c ? 'Expand sidebar' : 'Collapse sidebar';
    toggle.setAttribute('aria-label', toggle.title);
  };
  toggle.addEventListener('click', () => {
    const c = document.documentElement.dataset.sidebar !== 'collapsed';
    apply(c);
    onToggle(c);
  });
  apply(collapsed);
  root.append(...items, toggle);
}

export function setActiveNav(hash: string): void {
  const section = '#/' + (hash.replace(/^#\//, '').split(/[/?]/)[0] || 'home');
  for (const a of items) {
    if (a.getAttribute('href') === section) a.setAttribute('aria-current', 'page');
    else a.removeAttribute('aria-current');
  }
}
