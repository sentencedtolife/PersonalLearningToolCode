// Map: tracks as rows, units as cells coloured by mastery. Hover shows the rules.
import { app, levels } from '../app';
import type { Unit } from '../curriculum';
import { LEVEL_NAMES, LEVEL_RULES, type Level } from '../progress/mastery';
import { navigate, type ViewFactory } from '../router';
import { button, h } from '../ui/components';

function tip(unit: Unit, level: Level): string {
  const lines = [`${unit.title} (${unit.id})`, `Now: ${LEVEL_RULES[level]}`];
  if (level < 3) lines.push(`Next: ${LEVEL_RULES[level + 1]}`);
  if (unit.prereqs.length) lines.push(`Builds on: ${unit.prereqs.join(', ')}`);
  return lines.join('\n');
}

export const mapView: ViewFactory = () => {
  const lv = levels();
  const detail = h('div', { 'aria-live': 'polite' });
  const cells: HTMLButtonElement[] = [];

  const select = (unit: Unit, cell: HTMLButtonElement) => {
    cells.forEach((c) => c.setAttribute('aria-pressed', String(c === cell)));
    const lessons = [...app.lessons.values()].filter((l) => l.meta.unit === unit.id).sort((a, b) => a.meta.tier - b.meta.tier || a.meta.id.localeCompare(b.meta.id));
    const level = lv.get(unit.id) ?? 0;
    detail.replaceChildren(h('div', { class: 'card stack' },
      h('h2', { style: 'margin: 0' }, unit.title),
      h('p', { class: 'muted small', style: 'margin: 0' }, `${unit.id} · ${LEVEL_NAMES[level]} · tier ${unit.tiers.join('–')}`),
      lessons.length
        ? h('div', { class: 'stack' }, ...lessons.map((l) => {
          const status = app.progress.lessons[l.meta.id]?.status ?? 'new';
          return button(`${l.meta.title} (${status === 'practiced' ? 'practised' : status === 'read' ? 'in progress' : 'new'})`, () => navigate(`#/lesson/${l.meta.id}`), { wide: true });
        }))
        : h('p', { class: 'muted' }, 'No lessons for this unit in your library yet.'),
      button('Ask Claude for a lesson on this unit', () => navigate(`#/ask?unit=${encodeURIComponent(unit.id)}`), { variant: lessons.length ? 'flat' : 'primary' })));
  };

  const rows = app.curriculum.tracks.map((t) => h('div', { class: 'map-row' },
    h('div', { class: 'map-track' }, h('span', { class: 'track-swatch', style: `background: var(--track-${t.code})` }), t.name),
    h('div', { class: 'map-cells' }, ...t.units.map((u) => {
      const level = lv.get(u.id) ?? 0;
      const cell = h('button', { class: `map-cell m-${level}`, type: 'button', 'data-tip': tip(u, level), 'aria-pressed': 'false', 'aria-label': `${u.title}: ${LEVEL_NAMES[level]}` }, u.title);
      cell.addEventListener('click', () => select(u, cell));
      cells.push(cell);
      return cell;
    }))));

  const legend = h('div', { class: 'legend' }, ...LEVEL_NAMES.map((name, i) =>
    h('span', { 'data-tip': LEVEL_RULES[i], tabindex: 0 }, h('span', { class: `track-swatch map-cell m-${i}`, style: 'padding: 0' }), name)));

  return {
    title: 'Map',
    el: h('div', { class: 'page' },
      h('h1', {}, 'Map'),
      h('p', { class: 'muted' }, 'Each cell is a unit. Its colour is your mastery level. Hover or focus a cell to see the rules; click it to see its lessons.'),
      legend, ...rows, detail),
  };
};
