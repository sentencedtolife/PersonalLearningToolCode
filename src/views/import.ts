// Import: paste (or drop / pick a file), validate, save. Errors block; warnings don't.
import { app, knownUnits, saveLessonFile, saveProgress } from '../app';
import { nextFreeId, replaceLessonId } from '../lesson/parse';
import type { Issue } from '../lesson/types';
import { errorReport, issueWhere, loadLesson } from '../lesson/validate';
import { copyText } from '../platform';
import { lessonProgress, pruneLessonQuestions } from '../progress/progress';
import { navigate, type ViewFactory } from '../router';
import { button, h } from '../ui/components';

function issueList(title: string, cls: string, issues: Issue[]): HTMLElement {
  return h('div', {},
    h('h3', { class: cls }, `${title} (${issues.length})`),
    h('ul', {}, ...issues.map((i) => h('li', {},
      h('span', { class: cls }, `${issueWhere(i)}, ${i.field}: `), i.message,
      i.fix ? h('div', { class: 'fix' }, `Fix: ${i.fix}`) : null))));
}

export const importView: ViewFactory = () => {
  const paste = h('textarea', { id: 'import-text', class: 'mono', rows: 14, spellcheck: 'false', placeholder: 'Paste the lesson from Claude here (chat text around it is fine), or drop a .lesson.md file.' });
  const fileInput = h('input', { type: 'file', accept: '.md,.lesson.md,text/markdown,text/plain', class: 'sr-only', tabindex: -1 });
  const report = h('div', { class: 'report', 'aria-live': 'polite' });
  const dropzone = h('div', { class: 'dropzone' }, paste);

  const show = (...children: (HTMLElement | null)[]) => report.replaceChildren(...children.filter((c): c is HTMLElement => !!c));

  const finish = async (id: string, text: string, warnings: Issue[], replaced: boolean) => {
    if (replaced) {
      const lesson = loadLesson(text, { knownUnits: knownUnits() }).lesson!;
      pruneLessonQuestions(app.progress, id, lesson.questions.map((q) => q.id));
    }
    await saveLessonFile(id, text);
    if (!replaced) lessonProgress(app.progress, id).imported = new Date().toISOString();
    await saveProgress();
    paste.value = '';
    const lesson = app.lessons.get(id)!;
    const open = button('Open lesson', () => navigate(`#/lesson/${id}`), { variant: 'primary', key: 'Enter' });
    show(
      h('div', { class: 'feedback correct' }, h('div', { class: 'feedback-title' }, `Imported: ${lesson.meta.title}`),
        h('p', {}, `Saved as ${id}.lesson.md${replaced ? ', replacing the old version. Progress was kept for questions that still exist' : ''}.`)),
      warnings.length ? issueList('Warnings (the lesson was still imported)', 'warn', warnings) : null,
      h('div', { class: 'q-actions' }, open));
    open.focus();
  };

  const runImport = async (input = paste.value) => {
    if (!input.trim()) { show(h('p', { class: 'muted' }, 'Paste a lesson first.')); return; }
    const r = loadLesson(input, { knownUnits: knownUnits() });
    if (!r.lesson) {
      const copyBtn = button('Copy error report', () => {
        void copyText(errorReport(r.errors)).then(() => { copyBtn.firstChild!.textContent = 'Copied. Paste it back to Claude.'; });
      }, { variant: 'primary' });
      show(
        h('p', {}, 'The lesson was not imported. Copy the error report and paste it back to Claude in the same chat; it will send a corrected lesson.'),
        h('div', { class: 'q-actions' }, copyBtn),
        issueList('Errors', 'err', r.errors),
        r.warnings.length ? issueList('Warnings', 'warn', r.warnings) : null);
      return;
    }
    const id = r.lesson.meta.id;
    const existing = app.lessons.get(id);
    if (!existing) { await finish(id, r.text, r.warnings, false); return; }

    const keepBoth = async () => {
      const newId = nextFreeId(id, (x) => app.lessons.has(x));
      const text = replaceLessonId(r.text, newId);
      const again = loadLesson(text, { knownUnits: knownUnits() });
      await finish(newId, text, again.warnings, false);
    };
    const replace = button('Replace', () => void finish(id, r.text, r.warnings, true), { variant: 'primary' });
    show(
      h('div', { class: 'feedback not-yet' },
        h('div', { class: 'feedback-title' }, 'Replace existing lesson?'),
        h('p', {}, `You already have a lesson with the id "${id}": ${existing.meta.title}. Replace keeps your progress for questions that still exist. Keep both saves the new one as "${nextFreeId(id, (x) => app.lessons.has(x))}".`)),
      h('div', { class: 'q-actions' }, replace, button('Keep both', () => void keepBoth())));
    replace.focus();
  };

  const readFile = async (file: File | undefined) => {
    if (!file) return;
    paste.value = await file.text();
    await runImport();
  };
  fileInput.addEventListener('change', () => void readFile(fileInput.files?.[0]).finally(() => (fileInput.value = '')));

  const el = h('div', { class: 'page stack' },
    h('h1', {}, 'Import a lesson'),
    dropzone,
    h('div', { class: 'row' },
      button('Import lesson', () => void runImport(), { variant: 'primary', key: 'Ctrl+Enter' }),
      button('Choose a .lesson.md file', () => fileInput.click(), { variant: 'flat' }),
      fileInput),
    report);

  // The whole screen is a drop target.
  el.addEventListener('dragover', (e) => { e.preventDefault(); dropzone.classList.add('drag-over'); });
  el.addEventListener('dragleave', (e) => { if (!el.contains(e.relatedTarget as Node)) dropzone.classList.remove('drag-over'); });
  el.addEventListener('drop', (e) => {
    e.preventDefault();
    dropzone.classList.remove('drag-over');
    void readFile(e.dataTransfer?.files?.[0]);
  });

  return {
    title: 'Import',
    el,
    mounted: () => paste.focus(),
    onKey(e) {
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { void runImport(); return true; }
      return false;
    },
  };
};

