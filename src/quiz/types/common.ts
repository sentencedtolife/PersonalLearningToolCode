// Shared contract for question type modules: a pure `check` and a DOM `render`.
import type { Question } from '../../lesson/types';

export interface CheckResult {
  correct: boolean;
  /** Extra text for a wrong answer, e.g. per-choice feedback. Markdown. */
  feedback?: string;
}

export type MarkState = 'correct' | 'not-yet' | 'reveal';

export interface Widget<A> {
  el: HTMLElement;
  /** The current answer, or null if nothing has been entered yet. */
  value(): A | null;
  lock(): void;
  /** After checking: highlight what was right / not yet. 'reveal' also shows the correct answer. */
  mark(state: MarkState): void;
  /** Called when the answer changes after a "Not yet", to clear the highlight. */
  clearMark(): void;
  focus(): void;
  onKey?(e: KeyboardEvent): boolean;
  /** true if the widget renders the prompt itself (fill puts inputs inside the prompt). */
  ownsPrompt?: boolean;
}

export interface RenderCtx {
  /** Enter inside a text input. */
  submit(): void;
  /** The answer changed. */
  changed(): void;
}

export interface TypeModule<Q extends Question, A> {
  check(q: Q, answer: A): CheckResult;
  render(q: Q, ctx: RenderCtx): Widget<A>;
  /** Markdown describing the correct answer, shown on reveal. */
  answerText(q: Q): string;
}

/** Trim, collapse inner whitespace, and lowercase unless case-sensitive. */
export function normalize(s: string, caseSensitive = false): string {
  const t = s.trim().replace(/\s+/g, ' ');
  return caseSensitive ? t : t.toLowerCase();
}

export function matchesAny(input: string, accepted: string[], caseSensitive = false): boolean {
  const n = normalize(input, caseSensitive);
  return accepted.some((a) => normalize(a, caseSensitive) === n);
}

/** Fisher–Yates shuffle. `avoid` re-shuffles (a few times) if the result equals the original order. */
export function shuffled<T>(items: T[], avoidOriginal = false, random: () => number = Math.random): T[] {
  for (let attempt = 0; attempt < 10; attempt++) {
    const a = [...items];
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(random() * (i + 1));
      [a[i], a[j]] = [a[j], a[i]];
    }
    if (!avoidOriginal || items.length < 2 || a.some((x, i) => x !== items[i])) return a;
  }
  return [...items].reverse();
}
