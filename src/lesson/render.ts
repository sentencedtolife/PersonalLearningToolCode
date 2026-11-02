// Paper Markdown -> sanitised HTML. Math via KaTeX, code highlighted lazily, ```svg blocks become figures.
import DOMPurify from 'dompurify';
import hljs from 'highlight.js/lib/core';
import bash from 'highlight.js/lib/languages/bash';
import ini from 'highlight.js/lib/languages/ini';
import json from 'highlight.js/lib/languages/json';
import plaintext from 'highlight.js/lib/languages/plaintext';
import python from 'highlight.js/lib/languages/python';
import katex from 'katex';
import MarkdownIt from 'markdown-it';
import type { StateBlock, StateInline } from 'markdown-it';
import { escapeHtml } from '../ui/components';
import { PRACTICE_HEADING } from './parse';
import type { GlossaryEntry, Lesson } from './types';

hljs.registerLanguage('python', python);
hljs.registerLanguage('json', json);
hljs.registerLanguage('bash', bash);
hljs.registerLanguage('ini', ini);
hljs.registerLanguage('plaintext', plaintext);
// highlight.js has no GDScript grammar; Python is close enough (HANDOFF section 3).
hljs.registerAliases(['gdscript', 'gd'], { languageName: 'python' });

// ---------- Math: $inline$ and $$block$$ ----------

function mathInline(state: StateInline, silent: boolean): boolean {
  const src = state.src;
  const start = state.pos;
  if (src[start] !== '$' || src[start + 1] === '$') return false;
  const first = src[start + 1];
  if (!first || /\s/.test(first)) return false;
  let end = start + 1;
  for (;;) {
    end = src.indexOf('$', end);
    if (end === -1) return false;
    if (src[end - 1] === '\\') { end++; continue; }
    break;
  }
  // "$5 and $10" is money, not math: the closing $ must follow a non-space and not precede a digit.
  if (/\s/.test(src[end - 1]) || /\d/.test(src[end + 1] ?? '')) return false;
  if (!silent) state.push('math_inline', 'math', 0).content = src.slice(start + 1, end);
  state.pos = end + 1;
  return true;
}

function mathBlock(state: StateBlock, startLine: number, endLine: number, silent: boolean): boolean {
  const lineText = (n: number) => state.src.slice(state.bMarks[n] + state.tShift[n], state.eMarks[n]);
  const first = lineText(startLine);
  if (!first.startsWith('$$')) return false;
  if (silent) return true;
  let content: string;
  let last = startLine;
  const rest = first.slice(2);
  if (rest.trim().endsWith('$$')) {
    content = rest.trim().slice(0, -2);
  } else {
    const lines = [rest];
    let found = false;
    while (++last < endLine) {
      const t = lineText(last);
      if (t.trim().endsWith('$$')) { lines.push(t.trim().slice(0, -2)); found = true; break; }
      lines.push(t);
    }
    if (!found) return false;
    content = lines.join('\n');
  }
  state.line = last + 1;
  const token = state.push('math_block', 'math', 0);
  token.block = true;
  token.content = content;
  token.map = [startLine, state.line];
  return true;
}

const md = new MarkdownIt({ html: true, linkify: false, typographer: false });
md.inline.ruler.after('escape', 'math_inline', mathInline);
md.block.ruler.before('fence', 'math_block', mathBlock, { alt: ['paragraph', 'reference', 'blockquote', 'list'] });
md.renderer.rules.math_inline = (tokens, idx) => katex.renderToString(tokens[idx].content, { throwOnError: false });
md.renderer.rules.math_block = (tokens, idx) => `<div class="math-block">${katex.renderToString(tokens[idx].content, { throwOnError: false, displayMode: true })}</div>\n`;

md.renderer.rules.fence = (tokens, idx) => {
  const t = tokens[idx];
  const lang = t.info.trim().split(/\s+/)[0].toLowerCase();
  if (lang === 'svg') {
    const svg = DOMPurify.sanitize(t.content, { USE_PROFILES: { svg: true, svgFilters: true } });
    return `<figure class="svg-figure">${svg}</figure>\n`;
  }
  const known = lang && hljs.getLanguage(lang) ? lang : 'plaintext';
  // Highlighting happens later, when the block scrolls into view (see highlightLazily).
  return `<pre class="code"><code data-lang="${known}">${escapeHtml(t.content)}</code></pre>\n`;
};

const SANITIZE = {
  FORBID_TAGS: ['style', 'form', 'input', 'button', 'textarea', 'select', 'iframe', 'object', 'embed'],
  ADD_ATTR: ['target'],
};

// isSupported is false outside a browser (unit tests), where there is nothing to sanitise.
if (DOMPurify.isSupported) {
  DOMPurify.addHook('afterSanitizeAttributes', (node) => {
    if (node.tagName === 'A' && /^https?:/i.test(node.getAttribute('href') ?? '')) {
      node.setAttribute('target', '_blank');
      node.setAttribute('rel', 'noopener noreferrer');
    }
  });
}

export function renderMarkdown(src: string): string {
  return DOMPurify.sanitize(md.render(src), SANITIZE) as string;
}

/** Like renderMarkdown, but a single paragraph is unwrapped (for choices and short labels). */
export function renderInline(src: string): string {
  return DOMPurify.sanitize(md.renderInline(src), SANITIZE) as string;
}

/**
 * The paper: inline questions become <div class="q-slot" data-qid="…">, practice questions are removed
 * (they live in Practice mode), and an empty "## Practice" section is dropped.
 */
export function renderPaper(lesson: Lesson): string {
  const practiceIds = new Set(lesson.questions.filter((q) => q.inPractice).map((q) => q.id));
  const lines = lesson.body.split('\n').filter((l) => {
    const m = /^<!--Q:(.*)-->$/.exec(l);
    return !(m && practiceIds.has(m[1]));
  });
  const pi = lines.findIndex((l) => PRACTICE_HEADING.test(l));
  if (pi !== -1 && lines.slice(pi + 1).every((l) => l.trim() === '')) lines.length = pi;
  const html = md.render(lines.join('\n')).replace(/<!--Q:(.*?)-->/g, (_m, id: string) => `<div class="q-slot" data-qid="${escapeHtml(id)}"></div>`);
  return DOMPurify.sanitize(html, SANITIZE) as string;
}

function highlightBlock(code: HTMLElement): void {
  if (code.dataset.done) return;
  code.dataset.done = '1';
  const lang = code.dataset.lang ?? 'plaintext';
  code.innerHTML = hljs.highlight(code.textContent ?? '', { language: lang, ignoreIllegals: true }).value;
  code.classList.add('hljs');
}

/** Highlights code blocks only when they come near the screen, so very long lessons stay fast. */
export function highlightLazily(root: HTMLElement): () => void {
  const blocks = [...root.querySelectorAll<HTMLElement>('pre.code code[data-lang]')];
  if (!('IntersectionObserver' in window)) { blocks.forEach(highlightBlock); return () => {}; }
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (e.isIntersecting) { highlightBlock(e.target as HTMLElement); io.unobserve(e.target); }
    }
  }, { rootMargin: '400px' });
  blocks.forEach((b) => io.observe(b));
  return () => io.disconnect();
}

export function highlightNow(root: HTMLElement): void {
  root.querySelectorAll<HTMLElement>('pre.code code[data-lang]').forEach(highlightBlock);
}

const SKIP = 'code, pre, a, .katex, .term, svg, .q-slot, h1, button, input, textarea, select';

/** Underlines glossary terms (dotted). Hover or focus shows the definition. Only the first use of each term is focusable. */
export function applyGlossary(root: HTMLElement, glossary: GlossaryEntry[]): void {
  if (!glossary.length) return;
  const defs = new Map(glossary.map((g) => [g.term.toLowerCase(), g.definition]));
  const terms = [...defs.keys()].sort((a, b) => b.length - a.length).map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  const re = new RegExp(`\\b(${terms.join('|')})(?:s|es)?\\b`, 'gi');
  const focused = new Set<string>();
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode: (n) => ((n.parentElement?.closest(SKIP) ?? null) ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT),
  });
  const nodes: Text[] = [];
  while (walker.nextNode()) nodes.push(walker.currentNode as Text);
  for (const node of nodes) {
    const text = node.data;
    re.lastIndex = 0;
    if (!re.test(text)) continue;
    re.lastIndex = 0;
    const frag = document.createDocumentFragment();
    let last = 0;
    for (const m of text.matchAll(re)) {
      const key = m[1].toLowerCase();
      frag.append(text.slice(last, m.index));
      const span = document.createElement('span');
      span.className = 'term';
      span.textContent = m[0];
      span.dataset.tip = defs.get(key)!;
      span.setAttribute('aria-label', `${m[0]}: ${defs.get(key)}`);
      if (!focused.has(key)) { span.tabIndex = 0; focused.add(key); }
      frag.append(span);
      last = m.index! + m[0].length;
    }
    frag.append(text.slice(last));
    node.replaceWith(frag);
  }
}
