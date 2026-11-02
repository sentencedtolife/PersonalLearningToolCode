// Settings: appearance, motion, and data actions.
import { app, resetGeneratorPrompt, saveSettings } from '../app';
import { openDataFolder, saveFileAs } from '../platform';
import { today } from '../progress/progress';
import { ACCENTS, type Accent, type Settings } from '../settings';
import { isTauri } from '../store/store';
import type { ViewFactory } from '../router';
import { button, h, segmented } from '../ui/components';

export const settingsView: ViewFactory = () => {
  const el = h('div', { class: 'page page-narrow' });
  const status = h('p', { class: 'small', 'aria-live': 'polite' });
  let confirmingReset = false;

  const say = (text: string, ok = true) => {
    status.textContent = text;
    status.style.color = ok ? 'var(--correct)' : 'var(--not-yet)';
  };

  const row = (label: string, help: string | null, control: HTMLElement) =>
    h('div', { class: 'setting' }, h('div', {}, h('div', { class: 'setting-label' }, label), help ? h('div', { class: 'setting-help' }, help) : null), control);

  const choice = <K extends keyof Settings>(key: K, options: [Settings[K], string][]) =>
    segmented(options.map(([value, label]) => ({
      label, active: app.settings[key] === value,
      onClick: () => { void saveSettings({ [key]: value } as Partial<Settings>).then(draw); },
    })));

  const ACCENT_NAMES: Record<Accent, string> = { frost: 'Frost', teal: 'Teal', blue: 'Blue', deep: 'Deep blue', green: 'Green', purple: 'Purple', orange: 'Orange' };
  const accentPicker = () => h('div', { class: 'swatches', role: 'radiogroup', 'aria-label': 'Accent colour' },
    ...ACCENTS.map((a) => h('button', {
      class: 'swatch', type: 'button', role: 'radio', 'aria-checked': String(app.settings.accent === a),
      title: ACCENT_NAMES[a], 'aria-label': ACCENT_NAMES[a], 'data-swatch': a,
      onclick: () => { void saveSettings({ accent: a }).then(draw); },
    })));

  const draw = () => {
    const motion = h('input', { type: 'checkbox', id: 'reduce-motion' });
    motion.checked = app.settings.reduceMotion;
    motion.addEventListener('change', () => void saveSettings({ reduceMotion: motion.checked }));

    const resetControl = confirmingReset
      ? h('div', { class: 'row' },
        button('Yes, reset it', () => {
          void resetGeneratorPrompt().then(() => { confirmingReset = false; draw(); say('The generator prompt is back to the default.'); });
        }, { variant: 'primary' }),
        button('Cancel', () => { confirmingReset = false; draw(); }, { variant: 'flat' }))
      : button('Reset generator prompt to default', () => { confirmingReset = true; draw(); });

    el.replaceChildren(
      h('h1', {}, 'Settings'),
      row('Theme', null, choice('theme', [['dark', 'Dark'], ['light', 'Light']])),
      row('Accent colour', 'Buttons, links and highlights.', accentPicker()),
      row('Density', 'Compact fits more on the screen.', choice('density', [['comfortable', 'Comfortable'], ['compact', 'Compact']])),
      row('Paper font', 'The font used for lesson text.', choice('paperFont', [['sans', 'Sans (Inter)'], ['serif', 'Serif (Source Serif)']])),
      row('Font size', null, choice('fontSize', [['S', 'S'], ['M', 'M'], ['L', 'L']])),
      ...(isTauri ? [row('Resize preview', 'How see-through the blue rectangle is when you drag the corner grip (or press Alt+F8).', choice('marqueeAlpha', [[0.15, 'Light'], [0.25, 'Medium'], [0.4, 'Strong']]))] : []),
      row('Reduce motion', 'Turns off all animations. They are also off when Windows asks apps to reduce motion.',
        h('label', { class: 'check', for: 'reduce-motion' }, motion, 'No animations')),
      row('Data folder', isTauri ? 'Lessons, progress, backups, curriculum.json and generator-prompt.md.' : 'In the browser version, data is kept in this browser (localStorage).',
        button('Open data folder', () => { void openDataFolder().then((ok) => !ok && say('There is no folder to open in the browser version.', false)); }, { variant: 'secondary' })),
      row('Generator prompt', 'Replaces your copy of generator-prompt.md with the one that came with the app.', resetControl),
      row('Backup', 'Saves a copy of progress.json wherever you choose.',
        button('Export progress backup', () => {
          void saveFileAs(`nordlys-progress-${today()}.json`, JSON.stringify(app.progress, null, 1))
            .then((path) => path && say(`Saved: ${path}`))
            .catch((err: Error) => say(`Could not save: ${err.message}`, false));
        })),
      status);
  };
  draw();
  return { title: 'Settings', el };
};
