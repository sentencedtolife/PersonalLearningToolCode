// One question attempt (2 tries, hints, reveal, self-rating) and run scoring (HANDOFF 7.1). No DOM.
import type { Question } from '../lesson/types';
import { RESULT_SCORE, type Result, type RunResult } from '../progress/progress';

export const MAX_TRIES = 2;
export type Outcome = 'correct' | 'not-yet' | 'revealed';
export type SelfRating = 'got' | 'partly' | 'not';

export class Attempt {
  tries = 0;
  hintsShown = 0;
  result: RunResult | null = null;

  constructor(readonly question: Question) {}

  get done(): boolean {
    return this.result !== null;
  }

  get hintsLeft(): number {
    return this.done ? 0 : this.question.hints.length - this.hintsShown;
  }

  /** Reveals the next hint, or null if there are none left. */
  nextHint(): string | null {
    if (this.hintsLeft <= 0) return null;
    return this.question.hints[this.hintsShown++];
  }

  /** Records one try. Correct first time with no hints = 'correct'; correct later = 'correct-hinted'; 2 wrong tries = 'wrong'. */
  submit(correct: boolean): Outcome {
    if (this.done) throw new Error('Question already answered');
    this.tries++;
    if (correct) {
      this.result = this.tries === 1 && this.hintsShown === 0 ? 'correct' : 'correct-hinted';
      return 'correct';
    }
    if (this.tries >= MAX_TRIES) {
      this.result = 'wrong';
      return 'revealed';
    }
    return 'not-yet';
  }

  /** "Show answer" counts as wrong. */
  reveal(): void {
    if (!this.done) this.result = 'wrong';
  }

  selfRate(r: SelfRating): void {
    this.result = r === 'got' ? 'self-got' : r === 'partly' ? 'self-partly' : 'self-not';
  }

  completeTask(): void {
    this.result = 'task';
  }
}

/** Tasks are not scored. score is points / total (0–1); 0 if nothing was scored. */
export function scoreRun(results: RunResult[]): { points: number; total: number; score: number } {
  const scored = results.filter((r): r is Result => r !== 'task');
  const points = scored.reduce((s, r) => s + RESULT_SCORE[r], 0);
  return { points, total: scored.length, score: scored.length ? points / scored.length : 0 };
}
