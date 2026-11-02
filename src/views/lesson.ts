// Lesson screen: Paper mode (reading + inline "Check yourself" cards) and Practice mode.
import { app, levels, saveProgress } from '../app';
import { trackName, unitById } from '../curriculum';
import { applyGlossary, highlightLazily, renderPaper } from '../lesson/render';
import type { Lesson } from '../lesson/types';
import { missingPrerequisites } from '../progress/next';
import { lessonProgress } from '../progress/progress';
import { questionCard, type Card } from '../quiz/card';
import { navigate, type View, type ViewFactory } from '../router';
import { button, chip, h, segmented, trackChip } from '../ui/components';
import { practiceMode } from './practice';

const PAPER_TYPE_NAMES: Record<string, string> = {
  concept: 'Concept', 'worked-example': 'Worked example', 'case-study': 'Case study', brief: 'Brief', reference: 'Reference',
};

/** A lesson reopens in the mode it was left in. */
export function lessonRoute(id: string): string {
  return app.progress.lessons[id]?.lastPosition?.view === 'practice' ? `#/lesson/${id}/practice` : `#/lesson/${id}`;
}

/** Marks the lesson as opened: new -> read, remembers its glossary terms and when it was opened. */
function openLesson(lesson: Lesson): void {
  const id = lesson.meta.id;
  const lp = lessonProgress(app.progress, id);
  if (lp.status === 'new') lp.status = 'read';
  lp.lastOpened = new Date().toISOString();
  for (const g of lesson.meta.glossary) {
    const key = g.term.toLowerCase();
    if (!app.progress.glossary[key]) app.progress.glossary[key] = { definition: g.definition, firstLesson: id };
  }
  if (!app.openedAt.has(id)) app.openedAt.set(id, Date.now());
  void saveProgress();
}

export function lessonHeader(lesson: Lesson): HTMLElement {
  const m = lesson.meta;
  const unit = unitById(app.curriculum, m.unit);
  const missing = missingPrerequisites(lesson, app.lessons, levels());
  const prereqs = m.prerequisites.map((p) => app.lessons.get(p)?.meta.title ?? unitById(app.curriculum, p)?.title ?? p);
  return h('header', { class: 'lesson-head' },
    h('div', { class: 'lesson-stripe', style: `background: var(--track-${m.track})` }),
    h('h1', {}, m.title),
    h('div', { class: 'lesson-chips' },
      trackChip(m.track, trackName(app.curriculum, m.track)),
      unit ? chip(unit.title) : null,
      chip(`Tier ${m.tier}`),
      chip(`Energy ${m.energy}`),
      chip(`~${m.minutes} min`),
      chip(PAPER_TYPE_NAMES[m.paper_type])),
    h('div', { class: 'done-box' }, h('strong', {}, 'Done means'), m.done_means),
    m.glossary.length ? h('div', { class: 'terms-row' }, h('span', {}, 'Terms in this lesson:'), ...m.glossary.map((g) => h('span', { class: 'chip term', tabindex: 0, 'data-tip': g.definition }, g.term))) : null,
    prereqs.length ? h('p', { class: 'prereq-note' }, `Builds on: ${prereqs.join(', ')}.`, missing.length ? ` (Not in your library yet: ${missing.join(', ')}.)` : '') : null);
}

function paperMode(lesson: Lesson): Omit<View, 'title' | 'actions'> {
  const id = lesson.meta.id;
  const paper = h('article', { class: 'paper', html: renderPaper(lesson) });
  const cards: Card[] = [];
  for (const slot of paper.querySelectorAll<HTMLElement>('.q-slot')) {
    const q = lesson.questions.find((x) => x.id === slot.dataset.qid);
    if (!q) continue;
    const card = questionCard({ question: q, meta: 'Check yourself' });
    card.el.classList.add('check-card');
    slot.replaceWith(card.el);
    cards.push(card);
  }
  applyGlossary(paper, lesson.meta.glossary);
  const stopHighlight = highlightLazily(paper);

  const n = lesson.questions.length;
  const el = h('div', { class: 'page' },
    lessonHeader(lesson),
    paper,
    h('div', { class: 'paper-end' },
      n ? button(`Start practice (${n} question${n === 1 ? '' : 's'})`, () => navigate(`#/lesson/${id}/practice`), { variant: 'primary', key: 'P' }) : h('p', { class: 'muted' }, 'This lesson has no questions.')));

  const content = document.getElementById('content')!;
  let timer = 0;
  const onScroll = () => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => {
      const max = content.scrollHeight - content.clientHeight;
      lessonProgress(app.progress, id).lastPosition = { view: 'paper', scroll: max > 0 ? Math.round((content.scrollTop / max) * 1000) / 1000 : 0 };
      void saveProgress();
    }, 400);
  };

  return {
    el,
    mounted() {
      const pos = lessonProgress(app.progress, id).lastPosition;
      if (pos?.view === 'paper' && pos.scroll > 0) {
        content.scrollTop = pos.scroll * (content.scrollHeight - content.clientHeight);
      }
      content.addEventListener('scroll', onScroll, { passive: true });
    },
    destroy() {
      content.removeEventListener('scroll', onScroll);
      window.clearTimeout(timer);
      stopHighlight();
    },
    onKey(e) {
      const card = cards.find((c) => c.el.contains(document.activeElement));
      if (card?.onKey(e)) return true;
      const typing = (e.target as HTMLElement).tagName === 'INPUT' || (e.target as HTMLElement).tagName === 'TEXTAREA';
      if (!typing && (e.key === 'p' || e.key === 'P') && !e.ctrlKey && !e.metaKey && n) { navigate(`#/lesson/${id}/practice`); return true; }
      return false;
    },
  };
}

export const lessonView: ViewFactory = (params) => {
  const lesson = app.lessons.get(params.id);
  if (!lesson) {
    return {
      title: 'Lesson not found',
      el: h('div', { class: 'page' }, h('h1', {}, 'Lesson not found'),
        h('p', { class: 'muted' }, `There is no lesson with the id "${params.id}" in your library.`),
        button('Open Library', () => navigate('#/library'), { variant: 'primary' })),
    };
  }
  openLesson(lesson);
  const id = lesson.meta.id;
  const practice = params.mode === 'practice';
  const toggle = segmented([
    { label: 'Paper', active: !practice, onClick: () => practice && navigate(`#/lesson/${id}`, { replace: true }) },
    { label: 'Practice', active: practice, onClick: () => !practice && navigate(`#/lesson/${id}/practice`, { replace: true }) },
  ]);
  const lp = lessonProgress(app.progress, id);
  if (practice) lp.lastPosition = { view: 'practice', scroll: lp.lastPosition?.scroll ?? 0 };
  else if (lp.lastPosition?.view === 'practice') lp.lastPosition = { view: 'paper', scroll: lp.lastPosition.scroll };
  const mode = practice ? practiceMode(lesson) : paperMode(lesson);
  return { title: lesson.meta.title, actions: [toggle], ...mode };
};
