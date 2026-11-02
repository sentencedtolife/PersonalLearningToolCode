// Glossary: every term from lessons you have opened, A–Z, with where it first appeared.
import { app } from '../app';
import { navigate, type ViewFactory } from '../router';
import { h } from '../ui/components';

export const glossaryView: ViewFactory = () => {
  const entries = Object.entries(app.progress.glossary).sort(([a], [b]) => a.localeCompare(b));
  const byLetter = new Map<string, typeof entries>();
  for (const e of entries) {
    const letter = /[a-z]/i.test(e[0][0]) ? e[0][0].toUpperCase() : '#';
    byLetter.set(letter, [...(byLetter.get(letter) ?? []), e]);
  }

  return {
    title: 'Glossary',
    el: h('div', { class: 'page page-narrow' },
      h('h1', {}, 'Glossary'),
      entries.length
        ? h('p', { class: 'muted' }, `${entries.length} term${entries.length === 1 ? '' : 's'} from the lessons you have opened.`)
        : h('p', { class: 'muted' }, 'Terms appear here when you open a lesson that defines them.'),
      ...[...byLetter.entries()].map(([letter, items]) => h('section', {},
        h('h2', { class: 'gloss-letter' }, letter),
        h('dl', {}, ...items.map(([term, g]) => {
          const lesson = app.lessons.get(g.firstLesson);
          return h('div', { class: 'gloss-item' },
            h('dt', {}, term),
            h('dd', {}, g.definition, ' ',
              lesson
                ? h('a', { href: `#/lesson/${g.firstLesson}`, onclick: (e: Event) => { e.preventDefault(); navigate(`#/lesson/${g.firstLesson}`); } }, `First in: ${lesson.meta.title}`)
                : h('span', { class: 'faint' }, `First in: ${g.firstLesson}`)));
        }))))),
  };
};
