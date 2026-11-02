// ParsedLesson -> errors and warnings (with line numbers and suggested fixes) -> typed Lesson.
// Errors block import. Warnings don't.
import { normalizeText, parseLesson, stripOuterFence, type ParsedLesson, type RawBlock } from './parse';
import {
  FORMAT_VERSION, PAPER_TYPES, QUESTION_TYPES, TRACKS, letter, letterIndex,
  type Issue, type Lesson, type LessonMeta, type Question, type QuestionType,
} from './types';

export interface ValidateOptions {
  /** Unit ids from curriculum.json. If given, an unknown unit is a warning. */
  knownUnits?: Set<string>;
}

export interface ValidationResult {
  errors: Issue[];
  warnings: Issue[];
  /** Present only when there are no errors. */
  lesson: Lesson | null;
}

export const ID_PATTERN = /^[a-z0-9][a-z0-9.-]*$/;
const QID_PATTERN = /^[A-Za-z0-9_-]+$/;
export const BLANK_PATTERN = /_{3,}/g;

const META_REQUIRED = ['id', 'title', 'track', 'unit', 'tier', 'energy', 'minutes', 'paper_type', 'done_means', 'summary', 'version'] as const;
const META_KNOWN = new Set<string>([...META_REQUIRED, 'prerequisites', 'tags', 'glossary', 'seed']);
const META_EXAMPLE: Record<string, string> = {
  id: 'id: c-core-loops-01',
  title: 'title: "Core Loops"',
  track: 'track: C',
  unit: 'unit: C.loops',
  tier: 'tier: 1',
  energy: 'energy: 2',
  minutes: 'minutes: 25',
  paper_type: 'paper_type: concept',
  done_means: 'done_means: "You can …"',
  summary: 'summary: "One sentence about the lesson."',
  version: 'version: 1',
};

const Q_COMMON = ['id', 'type', 'prompt', 'hints', 'explanation', 'tags'];
const Q_FIELDS: Record<QuestionType, string[]> = {
  mcq: ['choices', 'answer', 'feedback'],
  multi: ['choices', 'answer'],
  numeric: ['answer', 'tolerance', 'unit'],
  text: ['accept', 'case_sensitive'],
  fill: ['blanks', 'case_sensitive'],
  order: ['items'],
  match: ['pairs'],
  reflect: ['model_answer', 'rubric'],
  task: ['instructions', 'done_means'],
};

const QUOTE_FIX = 'Put the text in double quotes. A colon followed by a space makes YAML read it as "key: value".';

class Collector {
  errors: Issue[] = [];
  warnings: Issue[] = [];
  error(line: number, field: string, message: string, fix: string): void {
    this.errors.push({ severity: 'error', line, field, message, fix });
  }
  warn(line: number, field: string, message: string, fix: string): void {
    this.warnings.push({ severity: 'warning', line, field, message, fix });
  }
}

function isScalar(v: unknown): v is string | number | boolean {
  return typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean';
}
function isText(v: unknown): v is string {
  return typeof v === 'string' && v.trim() !== '';
}
function show(v: unknown): string {
  if (typeof v === 'string') return `"${v}"`;
  try { return JSON.stringify(v) ?? String(v); } catch { return String(v); }
}
function isObject(v: unknown): v is Record<string, unknown> {
  return !!v && typeof v === 'object' && !Array.isArray(v);
}
function letterRange(n: number): string {
  return n <= 1 ? 'A' : `A–${letter(n - 1)}`;
}

/** Normalises text, removes an outer fence, parses and validates. */
export function loadLesson(text: string, opts: ValidateOptions = {}): ValidationResult & { text: string } {
  const clean = stripOuterFence(normalizeText(text));
  return { ...validateLesson(parseLesson(clean), opts), text: clean };
}

export function validateLesson(parsed: ParsedLesson, opts: ValidateOptions = {}): ValidationResult {
  const c = new Collector();
  for (const i of parsed.issues) c.errors.push(i);

  const meta = parsed.meta;
  if (meta) validateMeta(meta, parsed.metaLines, c, opts);

  const seen = new Map<string, number>();
  parsed.blocks.forEach((b, i) => validateBlock(b, i, seen, c));

  if (parsed.blocks.length === 0) {
    c.warn(0, 'questions', 'The lesson has no questions.', 'Add question blocks (~~~question … ~~~), and put most of them under "## Practice".');
  } else if (!parsed.hasPractice) {
    c.warn(0, 'body', 'There is no "## Practice" heading.', 'Add a line "## Practice" before the practice questions.');
  }

  const lesson = c.errors.length === 0 && meta ? buildLesson(parsed) : null;
  const byLine = (a: Issue, b: Issue) => a.line - b.line;
  return { errors: c.errors.sort(byLine), warnings: c.warnings.sort(byLine), lesson };
}

function validateMeta(meta: Record<string, unknown>, lines: Record<string, number>, c: Collector, opts: ValidateOptions): void {
  const at = (k: string) => lines[k] ?? 1;

  for (const k of META_REQUIRED) {
    const v = meta[k];
    if (v === undefined || v === null || v === '') {
      c.error(1, k, `Missing required field "${k}".`, `Add a frontmatter line like: ${META_EXAMPLE[k]}`);
    }
  }
  for (const k of Object.keys(meta)) {
    if (!META_KNOWN.has(k)) c.warn(at(k), k, `Unknown field "${k}". It will be ignored.`, 'Remove it, or check the spelling.');
  }

  const id = meta.id;
  if (id !== undefined && id !== null && id !== '' && !(isScalar(id) && ID_PATTERN.test(String(id)))) {
    c.error(at('id'), 'id', `${show(id)} is not a valid id.`, 'Use only lowercase letters, digits, "-" and ".", starting with a letter or digit, e.g. p-signals-01.');
  }
  for (const k of ['title', 'done_means', 'summary'] as const) {
    const v = meta[k];
    if (v !== undefined && v !== null && v !== '' && !isText(v)) c.error(at(k), k, `${k} must be text.`, QUOTE_FIX);
  }
  const track = meta.track;
  if (track !== undefined && track !== null && track !== '' && !TRACKS.includes(track as never)) {
    c.error(at('track'), 'track', `${show(track)} must be one of C, X, P, D, A, S, N (or K for capstones).`, 'Use the one-letter track code, e.g. track: C');
  }
  const unit = meta.unit;
  if (unit !== undefined && unit !== null && unit !== '') {
    if (typeof unit !== 'string') {
      c.error(at('unit'), 'unit', 'unit must be text, e.g. C.loops.', 'Use a unit id from curriculum_units.');
    } else {
      if (opts.knownUnits && !opts.knownUnits.has(unit)) {
        c.warn(at('unit'), 'unit', `"${unit}" is not a unit in curriculum.json.`, 'Use a unit id from curriculum_units, e.g. C.loops.');
      }
      if (typeof track === 'string' && TRACKS.includes(track as never) && !unit.startsWith(`${track}.`)) {
        c.warn(at('unit'), 'unit', `Unit "${unit}" does not belong to track ${track}.`, `Use a unit that starts with "${track}.", or change the track.`);
      }
    }
  }
  intField(meta, 'tier', 0, 4, at, c);
  intField(meta, 'energy', 1, 3, at, c);
  intField(meta, 'minutes', 1, 600, at, c);
  const pt = meta.paper_type;
  if (pt !== undefined && pt !== null && pt !== '' && !PAPER_TYPES.includes(pt as never)) {
    c.error(at('paper_type'), 'paper_type', `${show(pt)} must be one of ${PAPER_TYPES.join(', ')}.`, 'e.g. paper_type: concept');
  }
  const version = meta.version;
  if (version !== undefined && version !== null && version !== '' && version !== FORMAT_VERSION) {
    c.error(at('version'), 'version', `version ${show(version)} is not supported. This app reads format version ${FORMAT_VERSION}.`, `Set version: ${FORMAT_VERSION}`);
  }
  for (const k of ['prerequisites', 'tags'] as const) {
    const v = meta[k];
    if (v === undefined || v === null) continue;
    if (!Array.isArray(v) || !v.every(isScalar)) c.error(at(k), k, `${k} must be a list of text items.`, `e.g. ${k}: [${k === 'tags' ? 'core-loop' : 'C.loops'}]`);
  }
  const glossary = meta.glossary;
  if (glossary !== undefined && glossary !== null) {
    if (!Array.isArray(glossary)) {
      c.error(at('glossary'), 'glossary', 'glossary must be a list of {term, definition}.', 'Write each entry as "- term: …" followed by "  definition: …".');
    } else {
      const terms = new Set<string>();
      glossary.forEach((g, i) => {
        if (!isObject(g) || !isText(g.term) || !isText(g.definition)) {
          c.error(at('glossary'), `glossary[${i + 1}]`, `Glossary entry ${i + 1} needs both "term" and "definition" as text.`, 'Write it as "- term: …" followed by "  definition: …".');
          return;
        }
        const t = g.term.trim().toLowerCase();
        if (terms.has(t)) c.warn(at('glossary'), `glossary[${i + 1}]`, `The term "${g.term}" is listed twice.`, 'Remove the duplicate entry.');
        terms.add(t);
      });
    }
  }
  const seed = meta.seed;
  if (seed !== undefined && seed !== null) {
    if (!isObject(seed)) {
      c.error(at('seed'), 'seed', 'seed must be {theme, verb, constraint}.', 'e.g. seed: {theme: lighthouse, verb: push, constraint: "one button"}');
    } else {
      for (const [k, v] of Object.entries(seed)) {
        if (!['theme', 'verb', 'constraint'].includes(k)) c.warn(at('seed'), `seed.${k}`, `Unknown seed field "${k}".`, 'Use only theme, verb and constraint.');
        else if (!isScalar(v)) c.error(at('seed'), `seed.${k}`, `seed.${k} must be text.`, QUOTE_FIX);
      }
    }
  }
}

function intField(meta: Record<string, unknown>, k: string, min: number, max: number, at: (k: string) => number, c: Collector): void {
  const v = meta[k];
  if (v === undefined || v === null || v === '') return;
  if (typeof v !== 'number' || !Number.isInteger(v) || v < min || v > max) {
    c.error(at(k), k, `${show(v)} must be a whole number from ${min} to ${max}.`, `e.g. ${k}: ${min === 0 ? 1 : min}`);
  }
}

function validateBlock(b: RawBlock, index: number, seen: Map<string, number>, c: Collector): void {
  if (b.data === undefined) return; // YAML syntax error, already reported
  const label = `question at line ${b.line}`;
  if (!isObject(b.data)) {
    c.error(b.line, label, 'A question block must contain "field: value" lines.', 'Start with id, type, prompt and explanation.');
    return;
  }
  const q = b.data;
  const at = (k: string) => b.fieldLines[k] ?? b.line;

  let name = `q#${index + 1}`;
  if (q.id === undefined || q.id === null || q.id === '') {
    c.error(b.line, label, 'Missing required field "id".', `Add a line like: id: q${index + 1}`);
  } else if (!isScalar(q.id) || !QID_PATTERN.test(String(q.id))) {
    c.error(at('id'), label, `${show(q.id)} is not a valid question id.`, 'Use letters, digits, "-" or "_", e.g. q3.');
  } else {
    name = String(q.id);
    if (seen.has(name)) c.error(at('id'), `${name}.id`, `The question id "${name}" is already used at line ${seen.get(name)}.`, 'Give every question a different id: q1, q2, q3, …');
    else seen.set(name, at('id'));
  }

  const type = q.type;
  const typeOk = QUESTION_TYPES.includes(type as never);
  if (type === undefined || type === null || type === '') {
    c.error(b.line, `${name}.type`, 'Missing required field "type".', `Add a line like: type: mcq (one of ${QUESTION_TYPES.join(', ')}).`);
  } else if (!typeOk) {
    c.error(at('type'), `${name}.type`, `${show(type)} is not a question type.`, `Use one of: ${QUESTION_TYPES.join(', ')}.`);
  }
  for (const k of ['prompt', 'explanation'] as const) {
    if (q[k] === undefined || q[k] === null || q[k] === '') c.error(b.line, `${name}.${k}`, `Missing required field "${k}".`, k === 'prompt' ? 'Add prompt: "…" with the question text.' : 'Add explanation: "…" that says WHY the answer is right.');
    else if (!isText(q[k])) c.error(at(k), `${name}.${k}`, `${k} must be text.`, QUOTE_FIX);
  }
  if (q.hints !== undefined && q.hints !== null) {
    if (!Array.isArray(q.hints) || !q.hints.every(isText)) c.error(at('hints'), `${name}.hints`, 'hints must be a list of text items.', 'Write each hint on its own line starting with "- ".');
    else if (q.hints.length > 3) c.error(at('hints'), `${name}.hints`, `There are ${q.hints.length} hints. The maximum is 3.`, 'Keep the 3 most useful hints: a nudge, a narrowing, a near-answer.');
  }
  if (q.tags !== undefined && q.tags !== null && (!Array.isArray(q.tags) || !q.tags.every(isScalar))) {
    c.error(at('tags'), `${name}.tags`, 'tags must be a list of text items.', 'e.g. tags: [vectors]');
  }
  if (!typeOk) return;

  const t = type as QuestionType;
  const known = new Set([...Q_COMMON, ...Q_FIELDS[t]]);
  for (const k of Object.keys(q)) {
    if (!known.has(k)) c.warn(at(k), `${name}.${k}`, `"${k}" is not a field of ${t} questions. It will be ignored.`, `${t} questions use: ${Q_FIELDS[t].join(', ')}.`);
  }
  const f = (k: string) => `${name}.${k}`;
  const required = (k: string, hint: string): boolean => {
    if (q[k] === undefined || q[k] === null || q[k] === '') { c.error(b.line, f(k), `Missing required field "${k}" for a ${t} question.`, hint); return false; }
    return true;
  };

  switch (t) {
    case 'mcq':
    case 'multi': {
      const n = checkList(q.choices, 'choices', 2, 6, at, f, c, required);
      if (t === 'mcq') {
        if (!required('answer', 'Add answer: B (the letter of the correct choice; A is the first choice).')) break;
        if (typeof q.answer !== 'string' || letterIndex(q.answer) < 0) {
          c.error(at('answer'), f('answer'), `${show(q.answer)} must be a single letter.`, 'e.g. answer: B');
        } else if (n !== null && letterIndex(q.answer) >= n) {
          c.error(at('answer'), f('answer'), `"${q.answer}" is not one of the choices ${letterRange(n)}.`, `Use a letter from ${letterRange(n)}. A is the first choice.`);
        }
        if (q.feedback !== undefined && q.feedback !== null) {
          if (!isObject(q.feedback)) {
            c.error(at('feedback'), f('feedback'), 'feedback must map choice letters to text.', 'e.g. feedback:\n  A: "Why A is wrong."');
          } else {
            for (const [k, v] of Object.entries(q.feedback)) {
              if (letterIndex(k) < 0 || (n !== null && letterIndex(k) >= n)) c.error(at('feedback'), f(`feedback.${k}`), `"${k}" is not one of the choices${n !== null ? ` ${letterRange(n)}` : ''}.`, 'Use the letter of a wrong choice.');
              else if (!isText(v)) c.error(at('feedback'), f(`feedback.${k}`), `Feedback for ${k} must be text.`, QUOTE_FIX);
              else if (typeof q.answer === 'string' && k.toUpperCase() === q.answer.toUpperCase()) c.warn(at('feedback'), f(`feedback.${k}`), `${k} is the correct answer, so this feedback is never shown.`, 'Feedback is for wrong choices. Put the reasoning in explanation instead.');
            }
          }
        }
      } else {
        if (!required('answer', 'Add answer: [A, C] (the letters of all correct choices).')) break;
        if (!Array.isArray(q.answer)) {
          c.error(at('answer'), f('answer'), 'answer must be a list of letters.', 'e.g. answer: [A, C]');
        } else if (q.answer.length === 0) {
          c.error(at('answer'), f('answer'), 'answer must list at least one letter.', 'e.g. answer: [A, C]');
        } else {
          const used = new Set<string>();
          for (const a of q.answer) {
            if (typeof a !== 'string' || letterIndex(a) < 0) c.error(at('answer'), f('answer'), `${show(a)} must be a single letter.`, 'e.g. answer: [A, C]');
            else if (n !== null && letterIndex(a) >= n) c.error(at('answer'), f('answer'), `"${a}" is not one of the choices ${letterRange(n)}.`, `Use letters from ${letterRange(n)}.`);
            else if (used.has(a.toUpperCase())) c.error(at('answer'), f('answer'), `"${a}" is listed twice.`, 'List each correct letter once.');
            if (typeof a === 'string') used.add(a.toUpperCase());
          }
        }
      }
      break;
    }
    case 'numeric': {
      if (required('answer', 'Add answer: 42 (a number, no quotes).') && (typeof q.answer !== 'number' || !Number.isFinite(q.answer))) {
        c.error(at('answer'), f('answer'), `${show(q.answer)} must be a number.`, 'Write the number without quotes or units, e.g. answer: 3.5');
      }
      if (q.tolerance !== undefined && q.tolerance !== null && (typeof q.tolerance !== 'number' || q.tolerance < 0)) {
        c.error(at('tolerance'), f('tolerance'), `${show(q.tolerance)} must be a number of 0 or more.`, 'e.g. tolerance: 0.5');
      }
      if (q.unit !== undefined && q.unit !== null && !isScalar(q.unit)) c.error(at('unit'), f('unit'), 'unit must be text.', 'e.g. unit: "px"');
      break;
    }
    case 'text':
      checkList(q.accept, 'accept', 1, 50, at, f, c, required);
      boolField(q, 'case_sensitive', at, f, c);
      break;
    case 'fill': {
      boolField(q, 'case_sensitive', at, f, c);
      if (!required('blanks', 'Add blanks: with one list of accepted answers per ___ in the prompt.')) break;
      if (!Array.isArray(q.blanks)) { c.error(at('blanks'), f('blanks'), 'blanks must be a list of lists.', 'e.g. blanks:\n  - ["emit"]'); break; }
      q.blanks.forEach((bl, i) => {
        if (Array.isArray(bl)) {
          if (bl.length === 0 || !bl.every(isScalar)) c.error(at('blanks'), f(`blanks[${i + 1}]`), `Blank ${i + 1} must list at least one accepted answer as text.`, 'e.g. - ["emit"]');
        } else if (isScalar(bl)) {
          c.warn(at('blanks'), f(`blanks[${i + 1}]`), `Blank ${i + 1} should be a list.`, `Write it as - [${show(bl)}]`);
        } else {
          c.error(at('blanks'), f(`blanks[${i + 1}]`), `Blank ${i + 1} must be a list of accepted answers.`, 'e.g. - ["emit"]');
        }
      });
      if (typeof q.prompt === 'string') {
        const count = (q.prompt.match(BLANK_PATTERN) ?? []).length;
        if (count === 0) c.error(at('prompt'), f('prompt'), 'The prompt has no blanks.', 'Write ___ (three underscores) where each answer goes.');
        else if (count !== q.blanks.length) c.error(at('blanks'), f('blanks'), `The prompt has ${count} blank${count === 1 ? '' : 's'} (___) but blanks lists ${q.blanks.length}.`, 'Give one list of accepted answers per ___, in order.');
      }
      break;
    }
    case 'order': {
      const n = checkList(q.items, 'items', 3, 7, at, f, c, required);
      if (n !== null) {
        const texts = (q.items as unknown[]).map((v) => String(v).trim());
        if (new Set(texts).size !== texts.length) c.error(at('items'), f('items'), 'Two items have the same text, so the order is ambiguous.', 'Make every item different.');
      }
      break;
    }
    case 'match': {
      if (!required('pairs', 'Add pairs: with 3–6 items like - ["left", "right"].')) break;
      if (!Array.isArray(q.pairs)) { c.error(at('pairs'), f('pairs'), 'pairs must be a list of [left, right] pairs.', 'e.g. - ["Sprite2D", "Shows an image"]'); break; }
      if (q.pairs.length < 3 || q.pairs.length > 6) c.error(at('pairs'), f('pairs'), `There are ${q.pairs.length} pairs. Use 3 to 6.`, 'Add or remove pairs.');
      const lefts = new Set<string>();
      const rights = new Set<string>();
      q.pairs.forEach((p, i) => {
        if (!Array.isArray(p) || p.length !== 2 || !p.every(isScalar)) {
          c.error(at('pairs'), f(`pairs[${i + 1}]`), `Pair ${i + 1} must be exactly two text items.`, 'e.g. - ["Sprite2D", "Shows an image"]');
          return;
        }
        const [l, r] = p.map((v) => String(v).trim());
        if (lefts.has(l)) c.error(at('pairs'), f(`pairs[${i + 1}]`), `The left item "${l}" is used twice.`, 'Every left item must be different.');
        if (rights.has(r)) c.error(at('pairs'), f(`pairs[${i + 1}]`), `The right item "${r}" is used twice.`, 'Every right item must be different.');
        lefts.add(l);
        rights.add(r);
      });
      break;
    }
    case 'reflect':
      if (required('model_answer', 'Add model_answer: | followed by an example answer.') && !isText(q.model_answer)) c.error(at('model_answer'), f('model_answer'), 'model_answer must be text.', QUOTE_FIX);
      if (q.rubric !== undefined && q.rubric !== null && (!Array.isArray(q.rubric) || !q.rubric.every(isText))) c.error(at('rubric'), f('rubric'), 'rubric must be a list of text items.', 'Write each point on its own line starting with "- ".');
      break;
    case 'task':
      if (required('instructions', 'Add instructions: | with numbered steps.') && !isText(q.instructions)) c.error(at('instructions'), f('instructions'), 'instructions must be text.', QUOTE_FIX);
      if (required('done_means', 'Add done_means: "…" with the exact finish condition.') && !isText(q.done_means)) c.error(at('done_means'), f('done_means'), 'done_means must be text.', QUOTE_FIX);
      break;
  }
}

/** Checks a list of plain text items. Returns its length if usable, else null. */
function checkList(
  v: unknown, k: string, min: number, max: number,
  at: (k: string) => number, f: (k: string) => string, c: Collector,
  required: (k: string, hint: string) => boolean,
): number | null {
  if (!required(k, `Add ${k}: with ${min}–${max} items, each on its own line starting with "- ".`)) return null;
  if (!Array.isArray(v)) { c.error(at(k), f(k), `${k} must be a list.`, 'Write each item on its own line starting with "- ".'); return null; }
  let ok = true;
  v.forEach((item, i) => {
    if (!isScalar(item)) { c.error(at(k), f(`${k}[${i + 1}]`), `Item ${i + 1} of ${k} is not plain text.`, QUOTE_FIX); ok = false; }
  });
  if (v.length < min || v.length > max) {
    c.error(at(k), f(k), `There are ${v.length} ${k}. Use ${min} to ${max}.`, `Add or remove ${k}.`);
    ok = false;
  }
  return ok || v.length > 0 ? v.length : null;
}

function boolField(q: Record<string, unknown>, k: string, at: (k: string) => number, f: (k: string) => string, c: Collector): void {
  if (q[k] !== undefined && q[k] !== null && typeof q[k] !== 'boolean') c.error(at(k), f(k), `${k} must be true or false.`, `e.g. ${k}: true`);
}

const strs = (v: unknown): string[] => (Array.isArray(v) ? v.filter(isScalar).map((x) => String(x)) : []);
const str = (v: unknown): string => (isScalar(v) ? String(v) : '');

/** Only called when there are no errors, so fields are known to be well-formed. */
function buildLesson(parsed: ParsedLesson): Lesson {
  const m = parsed.meta!;
  const meta: LessonMeta = {
    id: String(m.id),
    title: str(m.title),
    track: m.track as LessonMeta['track'],
    unit: str(m.unit),
    tier: m.tier as number,
    energy: m.energy as 1 | 2 | 3,
    minutes: m.minutes as number,
    paper_type: m.paper_type as LessonMeta['paper_type'],
    done_means: str(m.done_means),
    summary: str(m.summary),
    prerequisites: strs(m.prerequisites),
    tags: strs(m.tags),
    glossary: Array.isArray(m.glossary) ? (m.glossary as { term: string; definition: string }[]).map((g) => ({ term: g.term.trim(), definition: g.definition.trim() })) : [],
    seed: isObject(m.seed) ? { theme: str(m.seed.theme) || undefined, verb: str(m.seed.verb) || undefined, constraint: str(m.seed.constraint) || undefined } : undefined,
    version: m.version as number,
  };

  const questions: Question[] = parsed.blocks.map((b) => {
    const q = b.data as Record<string, unknown>;
    const base = {
      id: String(q.id),
      prompt: str(q.prompt),
      hints: strs(q.hints),
      explanation: str(q.explanation),
      tags: strs(q.tags),
      inPractice: b.inPractice,
      line: b.line,
    };
    switch (q.type as QuestionType) {
      case 'mcq': return { ...base, type: 'mcq', choices: strs(q.choices), answer: str(q.answer).toUpperCase(), feedback: isObject(q.feedback) ? Object.fromEntries(Object.entries(q.feedback).map(([k, v]) => [k.toUpperCase(), str(v)])) : {} };
      case 'multi': return { ...base, type: 'multi', choices: strs(q.choices), answer: strs(q.answer).map((a) => a.toUpperCase()) };
      case 'numeric': return { ...base, type: 'numeric', answer: q.answer as number, tolerance: typeof q.tolerance === 'number' ? q.tolerance : 0, unit: isScalar(q.unit) ? String(q.unit) : undefined };
      case 'text': return { ...base, type: 'text', accept: strs(q.accept), case_sensitive: q.case_sensitive === true };
      case 'fill': return { ...base, type: 'fill', blanks: (q.blanks as unknown[]).map((bl) => (Array.isArray(bl) ? strs(bl) : [str(bl)])), case_sensitive: q.case_sensitive === true };
      case 'order': return { ...base, type: 'order', items: strs(q.items) };
      case 'match': return { ...base, type: 'match', pairs: (q.pairs as unknown[][]).map((p) => [str(p[0]), str(p[1])] as [string, string]) };
      case 'reflect': return { ...base, type: 'reflect', model_answer: str(q.model_answer), rubric: strs(q.rubric) };
      case 'task': return { ...base, type: 'task', instructions: str(q.instructions), done_means: str(q.done_means) };
    }
  });

  return { meta, body: parsed.body, questions };
}

export function formatIssue(i: Issue): string {
  return `${issueWhere(i)}, ${i.field}: ${i.message}${i.fix ? ` Fix: ${i.fix.replace(/\n/g, ' ')}` : ''}`;
}

export function errorReport(errors: Issue[]): string {
  return ['Fix these errors in the lesson file and output the full corrected lesson:', ...errors.map(formatIssue)].join('\n');
}

/** "line 14", or "whole lesson" for issues that are not about one line. */
export function issueWhere(i: Issue): string {
  return i.line > 0 ? `line ${i.line}` : 'whole lesson';
}
