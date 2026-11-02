// Home: one big "Next" button. Next asks for energy, then picks a lesson.
import { app, levels, saveSettings } from '../app';
import { pickNext } from '../progress/next';
import { navigate, type ViewFactory } from '../router';
import { button, energyPicker, h } from '../ui/components';
import { dueCount } from './review';

export const homeView: ViewFactory = () => {
  const el = h('div', { class: 'home' });
  let step: 'start' | 'energy' | 'none' = 'start';
  let chosen: 1 | 2 | 3 = app.settings.lastEnergy;

  const go = (energy: 1 | 2 | 3) => {
    app.energy = energy;
    chosen = energy;
    void saveSettings({ lastEnergy: energy });
    const pick = pickNext(app.lessons, app.progress, energy, levels());
    if (pick.kind === 'none') { step = 'none'; draw(); return; }
    navigate(`#/lesson/${pick.lessonId}`);
  };

  const inProgress = () => {
    const pick = pickNext(app.lessons, app.progress, 3, levels());
    return pick.kind === 'resume' ? app.lessons.get(pick.lessonId) ?? null : null;
  };

  const draw = () => {
    const notice = app.notice ? h('p', { class: 'notice' }, app.notice) : null;
    if (step === 'start') {
      const cont = inProgress();
      const due = dueCount();
      el.replaceChildren(
        notice ?? '',
        button('Next', () => { step = 'energy'; draw(); }, { variant: 'primary', big: true }),
        h('div', { class: 'home-secondary' },
          cont ? button(`Continue: ${cont.meta.title}`, () => navigate(`#/lesson/${cont.meta.id}`), { variant: 'flat' }) : null,
          due > 0 ? button(`Review (${due} due)`, () => navigate('#/review'), { variant: 'flat' }) : null));
      (el.querySelector('.btn-primary') as HTMLElement).focus();
    } else if (step === 'energy') {
      el.replaceChildren(
        h('h1', {}, 'How much energy do you have right now?'),
        energyPicker(chosen, go),
        h('p', { class: 'muted small' }, 'Press 1, 2 or 3. Esc goes back.'));
      (el.querySelector(`.energy-btn:nth-child(${chosen})`) as HTMLElement).focus();
    } else {
      el.replaceChildren(
        h('h1', {}, 'No lesson ready'),
        h('p', { class: 'muted' }, `There is no unpractised lesson for energy ${chosen} or lower in your library.`),
        button('No lesson ready. Ask Claude for one →', () => navigate(`#/ask?energy=${chosen}`), { variant: 'primary', key: 'Enter' }));
      (el.querySelector('.btn-primary') as HTMLElement).focus();
    }
  };

  return {
    title: 'Nordlys',
    el,
    mounted: () => { draw(); app.notice = null; },
    onKey(e) {
      if (e.ctrlKey || e.metaKey || e.altKey) return false;
      if (step !== 'none' && ['1', '2', '3'].includes(e.key)) { go(Number(e.key) as 1 | 2 | 3); return true; }
      if (e.key === 'Escape' && step !== 'start') { step = 'start'; draw(); return true; }
      return false;
    },
  };
};
