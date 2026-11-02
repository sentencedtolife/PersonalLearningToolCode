// Lesson format version 1. See content/generator-prompt.md (FORMAT SPEC) — the two must match.

export const FORMAT_VERSION = 1;
export const TRACKS = ['C', 'X', 'P', 'D', 'A', 'S', 'N', 'K'] as const;
export type Track = (typeof TRACKS)[number];
export const PAPER_TYPES = ['concept', 'worked-example', 'case-study', 'brief', 'reference'] as const;
export type PaperType = (typeof PAPER_TYPES)[number];
export const QUESTION_TYPES = ['mcq', 'multi', 'numeric', 'text', 'fill', 'order', 'match', 'reflect', 'task'] as const;
export type QuestionType = (typeof QUESTION_TYPES)[number];

export interface GlossaryEntry { term: string; definition: string }
export interface Seed { theme?: string; verb?: string; constraint?: string }

export interface LessonMeta {
  id: string;
  title: string;
  track: Track;
  unit: string;
  tier: number;
  energy: 1 | 2 | 3;
  minutes: number;
  paper_type: PaperType;
  done_means: string;
  summary: string;
  prerequisites: string[];
  tags: string[];
  glossary: GlossaryEntry[];
  seed?: Seed;
  version: number;
}

interface QBase {
  id: string;
  prompt: string;
  hints: string[];
  explanation: string;
  tags: string[];
  /** true if the block comes after the "## Practice" heading. */
  inPractice: boolean;
  /** 1-based line of the opening ~~~question fence. */
  line: number;
}

export interface McqQuestion extends QBase { type: 'mcq'; choices: string[]; answer: string; feedback: Record<string, string> }
export interface MultiQuestion extends QBase { type: 'multi'; choices: string[]; answer: string[] }
export interface NumericQuestion extends QBase { type: 'numeric'; answer: number; tolerance: number; unit?: string }
export interface TextQuestion extends QBase { type: 'text'; accept: string[]; case_sensitive: boolean }
export interface FillQuestion extends QBase { type: 'fill'; blanks: string[][]; case_sensitive: boolean }
export interface OrderQuestion extends QBase { type: 'order'; items: string[] }
export interface MatchQuestion extends QBase { type: 'match'; pairs: [string, string][] }
export interface ReflectQuestion extends QBase { type: 'reflect'; model_answer: string; rubric: string[] }
export interface TaskQuestion extends QBase { type: 'task'; instructions: string; done_means: string }

export type Question =
  | McqQuestion | MultiQuestion | NumericQuestion | TextQuestion | FillQuestion
  | OrderQuestion | MatchQuestion | ReflectQuestion | TaskQuestion;

export interface Lesson {
  meta: LessonMeta;
  /** Paper Markdown with each question block replaced by <!--Q:<id>-->. */
  body: string;
  questions: Question[];
}

export interface Issue {
  severity: 'error' | 'warning';
  /** 1-based line in the lesson text (after any outer fence was removed). 0 = whole file. */
  line: number;
  /** e.g. "track", "q2.answer", "frontmatter". */
  field: string;
  message: string;
  fix: string;
}

export function letter(i: number): string {
  return String.fromCharCode(65 + i);
}

export function letterIndex(l: string): number {
  return l.length === 1 ? l.toUpperCase().charCodeAt(0) - 65 : -1;
}
