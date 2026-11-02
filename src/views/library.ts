// Library: lessons grouped by track, filterable by track and status.
import { app } from '../app';
import { unitById } from '../curriculum';
import type { Lesson } from '../lesson/types';
import { navigate, type ViewFactory } from '../router';
import { button, chip, h } from '../ui/components';
import { lessonRoute } from './nav';

type Status = 'new' | 'read' | 'practiced';
const STATUS_LABEL: Record<Status, string> = { new: 'New', read: 'In progress', practiced: 'Practised' };

// Filters stay as they were while the app is open.
const filter = { track: 'all', status: 'all' as 'all' | Status };

function statusOf(l: Lesson): Status {
  return app.progress.lessons[l.meta.id]?.status ?? 'new';
}

export const libraryView: ViewFactory = () => {
  const list = h('div');
  const filtersEl = h('div', { class: 'filters' });
  const all = [...app.lessons.values()];
  const tracksWithLessons = app.curriculum.tracks.filter((t) => all.some((l) => l.meta.track === t.code));

  const chipRow = (label: string, options: { value: string; text: string }[], current: string, set: (v: string) => void) =>
    h('div', { class: 'row', role: 'group', 'aria-label': label },
      h('span', { class: 'small muted', style: 'min-width: 4em' }, label),
      ...options.map((o) => h('button', {
        class: 'chip', type: 'button', 'aria-pressed': String(current === o.value),
        onclick: () => { set(o.value); draw(); },
      }, o.text)));

  const draw = () => {
    filtersEl.replaceChildren(
      chipRow('Track', [{ value: 'all', text: 'All' }, ...tracksWithLessons.map((t) => ({ value: t.code, text: t.name }))], filter.track, (v) => (filter.track = v)),
      chipRow('Status', [{ value: 'all', text: 'All' }, ...(Object.keys(STATUS_LABEL) as Status[]).map((s) => ({ value: s, text: STATUS_LABEL[s] }))], filter.status, (v) => (filter.status = v as typeof filter.status)));

    const shown = all.filter((l) => (filter.track === 'all' || l.meta.track === filter.track) && (filter.status === 'all' || statusOf(l) === filter.status));
    const groups = app.curriculum.tracks
      .map((t) => ({ t, lessons: shown.filter((l) => l.meta.track === t.code).sort((a, b) => a.meta.tier - b.meta.tier || a.meta.title.localeCompare(b.meta.title)) }))
      .filter((g) => g.lessons.length);

    list.replaceChildren(
      ...groups.map(({ t, lessons }) => h('section', { class: 'lib-group' },
        h('h2', {}, h('span', { class: 'track-swatch', style: `background: var(--track-${t.code})` }), t.name, h('span', { class: 'muted small' }, `(${lessons.length})`)),
        ...lessons.map((l) => h('button', { class: 'card lib-item', type: 'button', onclick: () => navigate(lessonRoute(l.meta.id)) },
          h('div', { class: 'lib-item-title' }, l.meta.title),
          h('div', { class: 'muted small' }, l.meta.summary),
          h('div', { class: 'lib-item-meta' },
            chip(STATUS_LABEL[statusOf(l)]),
            chip(`Tier ${l.meta.tier}`),
            chip(`Energy ${l.meta.energy}`),
            chip(`~${l.meta.minutes} min`),
            chip(unitById(app.curriculum, l.meta.unit)?.title ?? l.meta.unit)))))),
      ...(groups.length ? [] : [h('p', { class: 'muted' }, all.length ? 'No lessons match these filters.' : 'Your library is empty.')]));
  };
  draw();

  return {
    title: 'Library',
    el: h('div', { class: 'page' },
      h('div', { class: 'row' }, h('h1', { style: 'margin: 0' }, 'Library'), h('span', { class: 'spacer' }), button('Ask Claude for a lesson', () => navigate('#/ask'), { variant: 'flat' })),
      app.broken.length ? h('p', { class: 'notice' }, `${app.broken.length} lesson file${app.broken.length === 1 ? ' has' : 's have'} errors and ${app.broken.length === 1 ? 'is' : 'are'} not shown: ${app.broken.map((b) => b.file).join(', ')}. Fix or re-import ${app.broken.length === 1 ? 'it' : 'them'}.`) : null,
      filtersEl,
      list),
  };
};
