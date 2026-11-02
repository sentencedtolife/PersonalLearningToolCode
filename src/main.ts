// Boot, router setup and the global keyboard handler.
import '@fontsource/inter/latin-400.css';
import '@fontsource/inter/latin-600.css';
import '@fontsource/jetbrains-mono/latin-400.css';
import '@fontsource/source-serif-4/latin-400.css';
import '@fontsource/source-serif-4/latin-400-italic.css';
import '@fontsource/source-serif-4/latin-600.css';
import 'katex/dist/katex.min.css';
import './styles/nord.css';
import './styles/app.css';

import { app, loadApp, saveProgress, saveSettings } from './app';
import { openExternal } from './platform';
import { back, currentHash, currentView, navigate, route, startRouter } from './router';
import { initHeaderbar, setHeader } from './ui/headerbar';
import { initSidebar, setActiveNav } from './ui/sidebar';
import { askView } from './views/ask';
import { glossaryView } from './views/glossary';
import { homeView } from './views/home';
import { importView } from './views/import';
import { lessonView } from './views/lesson';
import { libraryView } from './views/library';
import { mapView } from './views/map';
import { reviewView } from './views/review';
import { settingsView } from './views/settings';

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
  route('/lesson/:id/:mode?', lessonView);
  route('/library', libraryView);
  route('/map', mapView);
  route('/review', reviewView);
  route('/ask', askView);
  route('/import', importView);
  route('/glossary', glossaryView);
  route('/settings', settingsView);

  installKeyboard();

  // Lesson links to websites open in the system browser, never inside the app window.
  document.addEventListener('click', (e) => {
    const a = (e.target as HTMLElement).closest('a[href]') as HTMLAnchorElement | null;
    if (a && /^https?:/i.test(a.getAttribute('href') ?? '')) {
      e.preventDefault();
      void openExternal(a.href);
    }
  });

  // Reopen exactly where the app was left.
  if (!location.hash && app.progress.resume.route && app.progress.resume.route !== '#/home') {
    history.replaceState(null, '', app.progress.resume.route);
  }
  const content = document.getElementById('content')!;
  startRouter(content, (hash, view) => {
    setHeader(view.title, view.actions ?? [], hash !== '#/home');
    setActiveNav(hash);
    if (app.progress.resume.route !== hash) {
      app.progress.resume.route = hash;
      void saveProgress();
    }
  });
}

void boot();
