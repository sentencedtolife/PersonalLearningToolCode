import { describe, expect, it } from 'vitest';
import { nextFreeId, normalizeText, parseLesson, replaceLessonId, stripOuterFence } from '../src/lesson/parse';
import { loadLesson } from '../src/lesson/validate';
import { readRepoFile } from './helpers';

const MINI = `---
id: t-mini-01
title: Mini
track: C
unit: C.loops
tier: 1
energy: 1
minutes: 5
paper_type: concept
done_means: "Done."
summary: "A tiny lesson."
version: 1
---

# Mini

Intro paragraph.

~~~question
id: q1
type: text
prompt: "Say hi."
accept: ["hi"]
explanation: "Because."
~~~

## Practice

~~~question
id: q2
type: text
prompt: "Say bye."
accept: ["bye"]
explanation: "Because."
~~~
`;

describe('frontmatter', () => {
  it('parses fields and records their lines', () => {
    const p = parseLesson(MINI);
    expect(p.issues).toEqual([]);
    expect(p.meta).toMatchObject({ id: 't-mini-01', title: 'Mini', track: 'C', tier: 1, energy: 1, version: 1 });
    expect(p.metaLines.id).toBe(2);
    expect(p.metaLines.summary).toBe(11);
  });

  it('reports a missing opening ---', () => {
    const p = parseLesson('id: x\n');
    expect(p.issues[0]).toMatchObject({ line: 1, field: 'frontmatter' });
  });

  it('reports a frontmatter that is never closed', () => {
    const p = parseLesson('---\nid: x\ntitle: y\n');
    expect(p.issues[0].message).toMatch(/never closed/);
  });

  it('reports YAML syntax errors with a line inside the frontmatter', () => {
    const p = parseLesson('---\nid: x\ntitle: "unterminated\nsummary: y\n---\n');
    expect(p.issues).toHaveLength(1);
    expect(p.issues[0].message).toMatch(/YAML syntax error/);
    expect(p.issues[0].line).toBeGreaterThanOrEqual(3);
    expect(p.issues[0].line).toBeLessThanOrEqual(5);
  });
});

describe('question blocks', () => {
  it('splits blocks, records lines and the practice flag, and leaves placeholders', () => {
    const p = parseLesson(MINI);
    expect(p.blocks).toHaveLength(2);
    expect(p.blocks[0]).toMatchObject({ line: 19, inPractice: false, placeholder: '<!--Q:q1-->' });
    expect(p.blocks[1]).toMatchObject({ line: 29, inPractice: true, placeholder: '<!--Q:q2-->' });
    expect(p.blocks[0].fieldLines).toMatchObject({ id: 20, type: 21, prompt: 22, accept: 23, explanation: 24 });
    expect(p.hasPractice).toBe(true);
    expect(p.body).toContain('<!--Q:q1-->');
    expect(p.body).toContain('<!--Q:q2-->');
    expect(p.body).not.toContain('~~~');
    expect(p.body).not.toContain('Say hi');
  });

  it('keeps GDScript backtick fences inside tilde question blocks', () => {
    const r = loadLesson(readRepoFile('content/lessons/p-godot-tour-01.lesson.md'));
    expect(r.errors).toEqual([]);
    const q2 = r.lesson!.questions.find((q) => q.id === 'q2')!;
    expect(q2.prompt).toContain('```gdscript');
    expect(q2.prompt).toContain('print("Hello from ", name)');
    // The paper's own code fences are untouched.
    expect(r.lesson!.body).toContain('```gdscript\nextends Node2D');
  });

  it('reports a block that is never closed', () => {
    const p = parseLesson(MINI + '\n~~~question\nid: q3\n');
    const last = p.issues.at(-1)!;
    expect(last.message).toMatch(/never closed/);
    expect(last.line).toBe(37);
  });
});

describe('text cleanup', () => {
  it('removes a BOM and converts Windows line endings', () => {
    const crlf = '﻿' + MINI.replace(/\n/g, '\r\n');
    const r = loadLesson(crlf);
    expect(r.errors).toEqual([]);
    expect(r.lesson!.meta.id).toBe('t-mini-01');
    expect(normalizeText('a\r\nb\rc')).toBe('a\nb\nc');
  });

  it('strips an outer fence and the chat text around it', () => {
    const pasted = `Here is your lesson!\n\n\`\`\`\`lesson\n${MINI}\`\`\`\`\n\nLet me know if you want changes.`;
    expect(stripOuterFence(pasted).trim()).toBe(MINI.trim());
    const r = loadLesson(pasted);
    expect(r.errors).toEqual([]);
    expect(r.lesson!.questions).toHaveLength(2);
  });

  it('accepts a fence of five backticks with no info string', () => {
    const pasted = `chat\n\`\`\`\`\`\n${MINI}\`\`\`\`\`\nmore chat`;
    expect(stripOuterFence(pasted).trim()).toBe(MINI.trim());
  });

  it('leaves text without an outer fence alone', () => {
    expect(stripOuterFence(MINI)).toBe(MINI);
  });
});

describe('import helpers', () => {
  it('replaceLessonId changes only the frontmatter id line', () => {
    const out = replaceLessonId(MINI, 't-mini-01-2');
    expect(out.split('\n')[1]).toBe('id: t-mini-01-2');
    expect(out.match(/^id: /gm)).toHaveLength(3); // the two question ids are untouched
    expect(loadLesson(out).lesson!.meta.id).toBe('t-mini-01-2');
  });
  it('nextFreeId appends -2, -3, …', () => {
    const taken = new Set(['a', 'a-2']);
    expect(nextFreeId('a', (x) => taken.has(x))).toBe('a-3');
    expect(nextFreeId('b', (x) => taken.has(x))).toBe('b-2');
  });
});
