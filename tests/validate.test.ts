import { describe, expect, it } from 'vitest';
import { errorReport, formatIssue, loadLesson } from '../src/lesson/validate';
import { readRepoFile } from './helpers';

const META: Record<string, string> = {
  id: 't-check-01',
  title: 'Check',
  track: 'C',
  unit: 'C.loops',
  tier: '1',
  energy: '1',
  minutes: '5',
  paper_type: 'concept',
  done_means: '"Done."',
  summary: '"A lesson for tests."',
  version: '1',
};

/** Builds a lesson. `meta` overrides/adds frontmatter lines (null removes one); `questions` are raw YAML blocks. */
function lesson(meta: Record<string, string | null> = {}, questions: string[] = ['id: q1\ntype: text\nprompt: "Hi?"\naccept: ["hi"]\nexplanation: "x"']): string {
  const m = { ...META, ...meta };
  const front = Object.entries(m).filter(([, v]) => v !== null).map(([k, v]) => `${k}: ${v}`).join('\n');
  return `---\n${front}\n---\n\n# T\n\n## Practice\n\n${questions.map((q) => `~~~question\n${q}\n~~~`).join('\n\n')}\n`;
}

function q(type: string, fields: string, id = 'q1'): string {
  return `id: ${id}\ntype: ${type}\nprompt: "Question?"\nexplanation: "Because."\n${fields}`;
}

const units = new Set(['C.loops', 'C.verbs', 'P.tour']);

function errorsOf(text: string): string[] {
  return loadLesson(text, { knownUnits: units }).errors.map((e) => `${e.field}: ${e.message}`);
}
function warningsOf(text: string): string[] {
  return loadLesson(text, { knownUnits: units }).warnings.map((e) => `${e.field}: ${e.message}`);
}

describe('valid lessons', () => {
  it('a minimal lesson has no errors or warnings', () => {
    const r = loadLesson(lesson(), { knownUnits: units });
    expect(r.errors).toEqual([]);
    expect(r.warnings).toEqual([]);
    expect(r.lesson!.meta.id).toBe('t-check-01');
    expect(r.lesson!.questions[0]).toMatchObject({ id: 'q1', type: 'text', inPractice: true, case_sensitive: false });
  });

  it('applies defaults (tolerance 0, empty hints/tags/feedback)', () => {
    const r = loadLesson(lesson({}, [q('numeric', 'answer: 3'), q('mcq', 'choices: ["a", "b"]\nanswer: a', 'q2')]));
    expect(r.errors).toEqual([]);
    expect(r.lesson!.questions[0]).toMatchObject({ tolerance: 0, hints: [], tags: [] });
    expect(r.lesson!.questions[1]).toMatchObject({ answer: 'A', feedback: {} });
  });
});

describe('frontmatter errors', () => {
  it.each([
    [{ summary: null }, 'summary: Missing required field "summary".'],
    [{ id: '"Has Spaces"' }, 'id: "Has Spaces" is not a valid id.'],
    [{ track: 'Design' }, 'track: "Design" must be one of C, X, P, D, A, S, N (or K for capstones).'],
    [{ tier: '5' }, 'tier: 5 must be a whole number from 0 to 4.'],
    [{ energy: '0' }, 'energy: 0 must be a whole number from 1 to 3.'],
    [{ minutes: '2.5' }, 'minutes: 2.5 must be a whole number from 1 to 600.'],
    [{ paper_type: 'essay' }, 'paper_type: "essay" must be one of concept, worked-example, case-study, brief, reference.'],
    [{ version: '2' }, 'version: version 2 is not supported. This app reads format version 1.'],
    [{ title: '[1, 2]' }, 'title: title must be text.'],
    [{ unit: '42' }, 'unit: unit must be text, e.g. C.loops.'],
    [{ tags: 'core-loop' }, 'tags: tags must be a list of text items.'],
    [{ prerequisites: '{a: 1}' }, 'prerequisites: prerequisites must be a list of text items.'],
    [{ glossary: '"x"' }, 'glossary: glossary must be a list of {term, definition}.'],
    [{ glossary: '[{term: x}]' }, 'glossary[1]: Glossary entry 1 needs both "term" and "definition" as text.'],
    [{ seed: '"x"' }, 'seed: seed must be {theme, verb, constraint}.'],
    [{ seed: '{theme: [a]}' }, 'seed.theme: seed.theme must be text.'],
  ])('%o', (meta, expected) => {
    expect(errorsOf(lesson(meta))).toContain(expected);
  });

  it('reports field errors on the line of the field', () => {
    const r = loadLesson(lesson({ track: 'Design' }));
    expect(r.errors[0]).toMatchObject({ line: 4, field: 'track' });
  });
});

describe('frontmatter warnings', () => {
  it('unknown unit is a warning, not an error', () => {
    const r = loadLesson(lesson({ unit: 'C.nothing' }), { knownUnits: units });
    expect(r.errors).toEqual([]);
    expect(r.warnings.map((w) => w.message)).toContain('"C.nothing" is not a unit in curriculum.json.');
    expect(r.lesson).not.toBeNull();
  });
  it('unit from another track', () => {
    expect(warningsOf(lesson({ unit: 'P.tour' }))).toContain('unit: Unit "P.tour" does not belong to track C.');
  });
  it('unknown field', () => {
    expect(warningsOf(lesson({ categry: 'x' }))).toContain('categry: Unknown field "categry". It will be ignored.');
  });
  it('duplicate glossary term', () => {
    expect(warningsOf(lesson({ glossary: '[{term: a, definition: b}, {term: A, definition: c}]' }))).toContain('glossary[2]: The term "A" is listed twice.');
  });
  it('no questions, or no Practice heading', () => {
    expect(warningsOf(lesson({}, []))).toContain('questions: The lesson has no questions.');
    const noPractice = lesson().replace('## Practice\n', '');
    expect(warningsOf(noPractice)).toContain('body: There is no "## Practice" heading.');
  });
});

describe('question errors (all types)', () => {
  it.each([
    ['type: text\nprompt: "x"\naccept: [a]\nexplanation: "x"', 'question at line 19: Missing required field "id".'],
    ['id: "q 1"\ntype: text\nprompt: "x"\naccept: [a]\nexplanation: "x"', 'question at line 19: "q 1" is not a valid question id.'],
    ['id: q1\nprompt: "x"\nexplanation: "x"', 'q1.type: Missing required field "type".'],
    ['id: q1\ntype: essay\nprompt: "x"\nexplanation: "x"', 'q1.type: "essay" is not a question type.'],
    ['id: q1\ntype: text\naccept: [a]\nexplanation: "x"', 'q1.prompt: Missing required field "prompt".'],
    ['id: q1\ntype: text\nprompt: "x"\naccept: [a]', 'q1.explanation: Missing required field "explanation".'],
    ['id: q1\ntype: text\nprompt: [x]\naccept: [a]\nexplanation: "x"', 'q1.prompt: prompt must be text.'],
    [q('text', 'accept: [a]\nhints: "one"'), 'q1.hints: hints must be a list of text items.'],
    [q('text', 'accept: [a]\nhints: [a, b, c, d]'), 'q1.hints: There are 4 hints. The maximum is 3.'],
    [q('text', 'accept: [a]\ntags: "x"'), 'q1.tags: tags must be a list of text items.'],
    ['not a mapping', 'question at line 19: A question block must contain "field: value" lines.'],
  ])('%s', (block, expected) => {
    expect(errorsOf(lesson({}, [block]))).toContain(expected);
  });

  it('duplicate question ids', () => {
    const errs = errorsOf(lesson({}, [q('text', 'accept: [a]'), q('text', 'accept: [b]')]));
    expect(errs).toContain('q1.id: The question id "q1" is already used at line 20.');
  });

  it('YAML syntax error inside a block', () => {
    const errs = errorsOf(lesson({}, ['id: q1\ntype: text\nprompt: "x\nexplanation: y']));
    expect(errs.some((e) => e.startsWith('question at line 19: YAML syntax error'))).toBe(true);
  });
});

describe('question errors (per type)', () => {
  it.each([
    ['mcq', 'answer: A', 'q1.choices: Missing required field "choices" for a mcq question.'],
    ['mcq', 'choices: "a, b"\nanswer: A', 'q1.choices: choices must be a list.'],
    ['mcq', 'choices: [a]\nanswer: A', 'q1.choices: There are 1 choices. Use 2 to 6.'],
    ['mcq', 'choices: [a, b, c, d, e, f, g]\nanswer: A', 'q1.choices: There are 7 choices. Use 2 to 6.'],
    ['mcq', 'choices:\n  - Title: options\n  - b\nanswer: A', 'q1.choices[1]: Item 1 of choices is not plain text.'],
    ['mcq', 'choices: [a, b, c]', 'q1.answer: Missing required field "answer" for a mcq question.'],
    ['mcq', 'choices: [a, b, c]\nanswer: AB', 'q1.answer: "AB" must be a single letter.'],
    ['mcq', 'choices: [a, b, c]\nanswer: E', 'q1.answer: "E" is not one of the choices A–C.'],
    ['mcq', 'choices: [a, b]\nanswer: A\nfeedback: "x"', 'q1.feedback: feedback must map choice letters to text.'],
    ['mcq', 'choices: [a, b]\nanswer: A\nfeedback: {D: "x"}', 'q1.feedback.D: "D" is not one of the choices A–B.'],
    ['mcq', 'choices: [a, b]\nanswer: A\nfeedback: {B: [x]}', 'q1.feedback.B: Feedback for B must be text.'],
    ['multi', 'choices: [a, b, c]\nanswer: A', 'q1.answer: answer must be a list of letters.'],
    ['multi', 'choices: [a, b, c]\nanswer: []', 'q1.answer: answer must list at least one letter.'],
    ['multi', 'choices: [a, b, c]\nanswer: [A, F]', 'q1.answer: "F" is not one of the choices A–C.'],
    ['multi', 'choices: [a, b, c]\nanswer: [A, A]', 'q1.answer: "A" is listed twice.'],
    ['multi', 'choices: [a, b, c]\nanswer: [A, 3]', 'q1.answer: 3 must be a single letter.'],
    ['numeric', 'tolerance: 1', 'q1.answer: Missing required field "answer" for a numeric question.'],
    ['numeric', 'answer: "five"', 'q1.answer: "five" must be a number.'],
    ['numeric', 'answer: 5\ntolerance: -1', 'q1.tolerance: -1 must be a number of 0 or more.'],
    ['numeric', 'answer: 5\nunit: [px]', 'q1.unit: unit must be text.'],
    ['text', 'case_sensitive: false', 'q1.accept: Missing required field "accept" for a text question.'],
    ['text', 'accept: []', 'q1.accept: There are 0 accept. Use 1 to 50.'],
    ['text', 'accept: [a]\ncase_sensitive: "yes"', 'q1.case_sensitive: case_sensitive must be true or false.'],
    ['fill', 'case_sensitive: true', 'q1.blanks: Missing required field "blanks" for a fill question.'],
    ['fill', 'blanks: "emit"', 'q1.blanks: blanks must be a list of lists.'],
    ['fill', 'blanks:\n  - []', 'q1.blanks[1]: Blank 1 must list at least one accepted answer as text.'],
    ['fill', 'blanks:\n  - {a: 1}', 'q1.blanks[1]: Blank 1 must be a list of accepted answers.'],
    ['fill', 'blanks:\n  - [emit]', 'q1.prompt: The prompt has no blanks.'],
    ['order', 'items: [a, b]', 'q1.items: There are 2 items. Use 3 to 7.'],
    ['order', 'items: [a, b, a]', 'q1.items: Two items have the same text, so the order is ambiguous.'],
    ['match', 'pairs: "x"', 'q1.pairs: pairs must be a list of [left, right] pairs.'],
    ['match', 'pairs: [[a, 1], [b, 2]]', 'q1.pairs: There are 2 pairs. Use 3 to 6.'],
    ['match', 'pairs: [[a, 1], [b, 2], [c]]', 'q1.pairs[3]: Pair 3 must be exactly two text items.'],
    ['match', 'pairs: [[a, 1], [a, 2], [c, 3]]', 'q1.pairs[2]: The left item "a" is used twice.'],
    ['match', 'pairs: [[a, 1], [b, 1], [c, 3]]', 'q1.pairs[2]: The right item "1" is used twice.'],
    ['reflect', 'rubric: [a]', 'q1.model_answer: Missing required field "model_answer" for a reflect question.'],
    ['reflect', 'model_answer: [a]', 'q1.model_answer: model_answer must be text.'],
    ['reflect', 'model_answer: "x"\nrubric: "a"', 'q1.rubric: rubric must be a list of text items.'],
    ['task', 'done_means: "x"', 'q1.instructions: Missing required field "instructions" for a task question.'],
    ['task', 'instructions: "x"', 'q1.done_means: Missing required field "done_means" for a task question.'],
    ['task', 'instructions: [x]\ndone_means: "x"', 'q1.instructions: instructions must be text.'],
  ])('%s: %s', (type, fields, expected) => {
    expect(errorsOf(lesson({}, [q(type, fields)]))).toContain(expected);
  });

  it('fill: blank count must match the prompt', () => {
    const block = 'id: q1\ntype: fill\nprompt: "a ___ b ___"\nexplanation: "x"\nblanks:\n  - [x]';
    expect(errorsOf(lesson({}, [block]))).toContain('q1.blanks: The prompt has 2 blanks (___) but blanks lists 1.');
  });

  it('per-type warnings: unknown field, feedback on the answer, string blank', () => {
    expect(warningsOf(lesson({}, [q('text', 'accept: [a]\nchoices: [x]')]))).toContain('q1.choices: "choices" is not a field of text questions. It will be ignored.');
    expect(warningsOf(lesson({}, [q('mcq', 'choices: [a, b]\nanswer: A\nfeedback: {A: "x"}')]))).toContain('q1.feedback.A: A is the correct answer, so this feedback is never shown.');
    const fill = 'id: q1\ntype: fill\nprompt: "a ___"\nexplanation: "x"\nblanks:\n  - emit';
    const r = loadLesson(lesson({}, [fill]));
    expect(r.errors).toEqual([]);
    expect(r.warnings.map((w) => w.message)).toContain('Blank 1 should be a list.');
    expect(r.lesson!.questions[0]).toMatchObject({ blanks: [['emit']] });
  });
});

describe('broken fixture', () => {
  const r = loadLesson(readRepoFile('tests/fixtures/broken.lesson.md'), { knownUnits: units });

  it.each([
    [1, 'summary', /Missing required field "summary"/],
    [2, 'id', /"Broken Lesson" is not a valid id/],
    [4, 'track', /"Design" must be one of/],
    [6, 'tier', /7 must be a whole number from 0 to 4/],
    [9, 'paper_type', /"essay" must be one of/],
    [11, 'version', /version 2 is not supported/],
    [26, 'q1.answer', /"E" is not one of the choices A–C/],
    [33, 'q1.id', /already used at line 19/],
    [34, 'q1.type', /"essay" is not a question type/],
    [43, 'q3.blanks', /2 blanks \(___\) but blanks lists 1/],
    [46, 'q3.hints', /4 hints. The maximum is 3/],
    [57, 'q4.answer', /"five" must be a number/],
    [65, 'q5.items', /2 items. Use 3 to 7/],
    [75, 'question at line 71', /YAML syntax error/],
    [79, 'question', /never closed/],
  ])('line %i, %s', (line, field, message) => {
    const hit = r.errors.find((e) => e.line === line && e.field === field && message.test(e.message));
    expect(hit, `expected an error at line ${line} on ${field}. Got:\n${r.errors.map(formatIssue).join('\n')}`).toBeDefined();
  });

  it('blocks import (no lesson) and reports at least 8 errors', () => {
    expect(r.lesson).toBeNull();
    expect(r.errors.length).toBeGreaterThanOrEqual(8);
  });

  it('builds an error report for pasting back to Claude', () => {
    const report = errorReport(r.errors);
    expect(report.split('\n')[0]).toBe('Fix these errors in the lesson file and output the full corrected lesson:');
    expect(report).toContain('line 26, q1.answer: "E" is not one of the choices A–C.');
    expect(report).toContain('line 4, track: "Design" must be one of C, X, P, D, A, S, N (or K for capstones).');
  });
});
