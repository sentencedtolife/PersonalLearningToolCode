// Ask: copies generator prompt + learner context + request, to paste into Claude.
import { app, levels, readGeneratorPrompt, saveSettings } from '../app';
import { unitById } from '../curriculum';
import { copyText } from '../platform';
import { buildContext } from '../progress/context';
import { today } from '../progress/progress';
import { navigate, type ViewFactory } from '../router';
import { button, energyPicker, h } from '../ui/components';

export async function buildPrompt(request: string, energy: number): Promise<string> {
  const prompt = await readGeneratorPrompt();
  const context = buildContext({
    date: today(), energy, request, lessons: [...app.lessons.values()], progress: app.progress, curriculum: app.curriculum, levels: levels(),
  });
  const req = request.trim() ? request.trim() : 'next (choose the next lesson in the curriculum, using the rules above)';
  return `${prompt.trimEnd()}\n\n${context}\n\nMy request: ${req}\n`;
}

export const askView: ViewFactory = (_params, query) => {
  const unit = query.get('unit') ? unitById(app.curriculum, query.get('unit')!) : undefined;
  const qEnergy = Number(query.get('energy'));
  let energy: 1 | 2 | 3 = qEnergy === 1 || qEnergy === 2 || qEnergy === 3 ? qEnergy : app.energy ?? app.settings.lastEnergy;

  const request = h('textarea', { id: 'ask-request', rows: 3, placeholder: 'e.g. teach me signals in Godot' });
  if (unit) request.value = `Teach me the unit ${unit.id} (${unit.title}).`;
  const status = h('p', { class: 'small', 'aria-live': 'polite' });
  const pickerSlot = h('div');
  const drawPicker = () => pickerSlot.replaceChildren(energyPicker(energy, (n) => { energy = n; app.energy = n; void saveSettings({ lastEnergy: n }); drawPicker(); }));
  drawPicker();

  const copy = async () => {
    try {
      await copyText(await buildPrompt(request.value, energy));
      status.textContent = 'Copied. Now paste it into a new Claude chat.';
      status.className = 'small';
      status.style.color = 'var(--correct)';
    } catch (err) {
      status.textContent = `Could not copy: ${(err as Error).message}`;
      status.style.color = 'var(--not-yet)';
    }
  };

  const el = h('div', { class: 'page page-narrow stack' },
    h('h1', {}, 'Ask Claude for a lesson'),
    h('label', { for: 'ask-request', class: 'setting-label' }, "What do you want to learn? (leave empty for 'next in curriculum')"),
    request,
    h('div', { class: 'setting-label' }, 'Energy'),
    pickerSlot,
    h('div', { class: 'stack center' },
      button('Copy prompt for Claude', () => void copy(), { variant: 'primary', big: true }),
      h('p', { class: 'muted' }, 'Paste into Claude. Copy the lesson it gives you. Then go to Import.'),
      status,
      button('Go to Import →', () => navigate('#/import'), { variant: 'flat' })));

  return {
    title: 'Ask',
    el,
    mounted: () => request.focus(),
    onKey(e) {
      const typing = (e.target as HTMLElement).tagName === 'TEXTAREA';
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { void copy(); return true; }
      if (!typing && !e.ctrlKey && !e.metaKey && !e.altKey && ['1', '2', '3'].includes(e.key)) {
        energy = Number(e.key) as 1 | 2 | 3;
        drawPicker();
        return true;
      }
      return false;
    },
  };
};
