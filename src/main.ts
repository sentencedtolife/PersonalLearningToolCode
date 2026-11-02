// Boot, router setup and the global keyboard handler.
import '@fontsource/inter/latin-400.css';
import '@fontsource/inter/latin-600.css';
import '@fontsource/jetbrains-mono/latin-400.css';
import '@fontsource/source-serif-4/latin-400.css';
import '@fontsource/source-serif-4/latin-400-italic.css';
import '@fontsource/source-serif-4/latin-600.css';
import './styles/nord.css';
import './styles/app.css';

import { app, loadApp, saveSettings } from './app';
import { back, currentHash, currentView, navigate, route, startRouter } from './router';
import { initHeaderbar, setHeader } from './ui/headerbar';
import { initSidebar, setActiveNav } from './ui/sidebar';
import { homeView } from './views/home';
import { placeholderView } from './views/placeholder';

const GO_KEYS: Record<string, string> = { h: '#/home', l: '#/library', m: '#/map', r: '#/review', a: '#/ask', i: '#/import' };

function isTyping(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el) return false;
  return el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.tagName === 'SELECT' || el.isContentEditable;
}

function installKeyboard(): void {
  let gPending = 0;
  document.addEventListener('keydown', (e) => {
    if (e.defaultPrevented) return;
    const typing = isTyping(e.target);
    const view = currentView();

    // "G then X" chords.
    if (gPending && !typing && !e.ctrlKey && !e.metaKey && !e.altKey) {
      const target = GO_KEYS[e.key.toLowerCase()];
      window.clearTimeout(gPending);
      gPending = 0;
      if (target) { e.preventDefault(); navigate(target); return; }
    }

    if (typing && e.key !== 'Escape' && !(e.key === 'Enter' && (e.ctrlKey || e.metaKey))) {
      // Let views see Enter inside single-line answer inputs.
      if (!(e.key === 'Enter' && (e.target as HTMLElement).tagName === 'INPUT')) return;
    }

    if (view?.onKey?.(e)) { e.preventDefault(); return; }

    if (e.key === 'Escape') {
      if (typing) { (e.target as HTMLElement).blur(); return; }
      if (currentHash() !== '#/home') { e.preventDefault(); back(); }
      return;
    }
    if (!typing && e.key.toLowerCase() === 'g' && !e.ctrlKey && !e.metaKey && !e.altKey) {
      gPending = window.setTimeout(() => { gPending = 0; }, 1500);
    }
  });
}

async function boot(): Promise<void> {
  await loadApp();
  await initHeaderbar(document.getElementById('headerbar')!, back);
  initSidebar(document.getElementById('sidebar')!, app.settings.sidebarCollapsed, (c) => void saveSettings({ sidebarCollapsed: c }));

  route('/home', homeView);
  route('/library', placeholderView('Library'));
  route('/map', placeholderView('Map'));
  route('/review', placeholderView('Review'));
  route('/ask', placeholderView('Ask'));
  route('/import', placeholderView('Import'));
  route('/glossary', placeholderView('Glossary'));
  route('/settings', placeholderView('Settings'));

  installKeyboard();
  const content = document.getElementById('content')!;
  startRouter(content, (hash, view) => {
    setHeader(view.title, view.actions ?? [], hash !== '#/home');
    setActiveNav(hash);
  });
}

void boot();
