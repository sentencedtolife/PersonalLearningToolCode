import { describe, expect, it } from 'vitest';
import type { FillQuestion, MatchQuestion, McqQuestion, MultiQuestion, NumericQuestion, OrderQuestion, TextQuestion } from '../src/lesson/types';
import { Attempt, scoreRun } from '../src/quiz/engine';
import { normalize, shuffled } from '../src/quiz/types/common';
import { fill } from '../src/quiz/types/fill';
import { match } from '../src/quiz/types/match';
import { mcq } from '../src/quiz/types/mcq';
import { multi } from '../src/quiz/types/multi';
import { numeric, parseNumber } from '../src/quiz/types/numeric';
import { order } from '../src/quiz/types/order';
import { reflect } from '../src/quiz/types/reflect';
import { task } from '../src/quiz/types/task';
import { text } from '../src/quiz/types/text';

const base = { prompt: 'x', explanation: 'x', hints: ['h1', 'h2'], tags: [], inPractice: true, line: 1 };

describe('mcq', () => {
  const q: McqQuestion = { ...base, id: 'q1', type: 'mcq', choices: ['a', 'b', 'c'], answer: 'B', feedback: { A: 'Not A because…' } };
  it('is correct only for the answer letter', () => {
    expect(mcq.check(q, 'B')).toEqual({ correct: true, feedback: undefined });
    expect(mcq.check(q, 'b').correct).toBe(true);
    expect(mcq.check(q, 'C')).toEqual({ correct: false, feedback: undefined });
  });
  it('returns per-choice feedback for a wrong choice', () => {
    expect(mcq.check(q, 'A')).toEqual({ correct: false, feedback: 'Not A because…' });
  });
  it('describes the answer with its letter and text', () => {
    expect(mcq.answerText(q)).toBe('**B.** b');
  });
});

describe('multi', () => {
  const q: MultiQuestion = { ...base, id: 'q1', type: 'multi', choices: ['a', 'b', 'c', 'd'], answer: ['A', 'C'] };
  it('needs exactly the answer set, in any order', () => {
    expect(multi.check(q, ['C', 'A']).correct).toBe(true);
    expect(multi.check(q, ['A']).correct).toBe(false);
    expect(multi.check(q, ['A', 'C', 'D']).correct).toBe(false);
    expect(multi.check(q, []).correct).toBe(false);
  });
});

describe('numeric', () => {
  const q: NumericQuestion = { ...base, id: 'q1', type: 'numeric', answer: 2.5, tolerance: 0.1, unit: 'px' };
  it('accepts values within the tolerance', () => {
    expect(numeric.check(q, '2.5').correct).toBe(true);
    expect(numeric.check(q, '2.6').correct).toBe(true);
    expect(numeric.check(q, '2.4').correct).toBe(true);
    expect(numeric.check(q, '2.7').correct).toBe(false);
  });
  it('accepts "," as the decimal separator and an optional unit', () => {
    expect(numeric.check(q, '2,5').correct).toBe(true);
    expect(numeric.check(q, ' 2.5 px ').correct).toBe(true);
  });
  it('tolerance 0 means exact', () => {
    expect(numeric.check({ ...q, answer: 5, tolerance: 0 }, '5').correct).toBe(true);
    expect(numeric.check({ ...q, answer: 5, tolerance: 0 }, '5.01').correct).toBe(false);
  });
  it('explains when the input is not a number', () => {
    expect(numeric.check(q, 'five')).toMatchObject({ correct: false, feedback: expect.stringMatching(/not a number/) });
    expect(parseNumber('')).toBeNull();
    expect(parseNumber('-3')).toBe(-3);
    expect(parseNumber('.5')).toBe(0.5);
    expect(parseNumber('1e3')).toBe(1000);
  });
});

describe('text', () => {
  const q: TextQuestion = { ...base, id: 'q1', type: 'text', accept: ['Core loop', 'loop'], case_sensitive: false };
  it('trims, collapses whitespace and ignores case', () => {
    expect(text.check(q, '  core   LOOP ').correct).toBe(true);
    expect(text.check(q, 'loop').correct).toBe(true);
    expect(text.check(q, 'core-loop').correct).toBe(false);
  });
  it('respects case_sensitive', () => {
    const cs = { ...q, case_sensitive: true };
    expect(text.check(cs, 'Core loop').correct).toBe(true);
    expect(text.check(cs, 'core loop').correct).toBe(false);
  });
  it('normalize()', () => {
    expect(normalize('  A \t B\n C ')).toBe('a b c');
  });
});

describe('fill', () => {
  const q: FillQuestion = { ...base, id: 'q1', type: 'fill', prompt: '`func ___():` and `.___ = 1`', blanks: [['_ready', '_ready()'], ['position']], case_sensitive: false };
  it('needs every blank to match one of its accepted answers', () => {
    expect(fill.check(q, ['_ready', 'position']).correct).toBe(true);
    expect(fill.check(q, [' _ready() ', 'POSITION']).correct).toBe(true);
    expect(fill.check(q, ['_ready', 'pos']).correct).toBe(false);
    expect(fill.check(q, ['_ready']).correct).toBe(false);
  });
});

describe('order', () => {
  const q: OrderQuestion = { ...base, id: 'q1', type: 'order', items: ['one', 'two', 'three'] };
  it('is correct only in the exact order', () => {
    expect(order.check(q, ['one', 'two', 'three']).correct).toBe(true);
    expect(order.check(q, ['two', 'one', 'three']).correct).toBe(false);
    expect(order.check(q, ['one', 'two']).correct).toBe(false);
  });
  it('shuffled(…, avoidOriginal) never returns the original order', () => {
    for (let i = 0; i < 50; i++) expect(shuffled(['a', 'b', 'c'], true)).not.toEqual(['a', 'b', 'c']);
    expect(shuffled(['a', 'b'], true, () => 0.99)).toEqual(['b', 'a']);
  });
});

describe('match', () => {
  const q: MatchQuestion = { ...base, id: 'q1', type: 'match', pairs: [['a', '1'], ['b', '2'], ['c', '3']] };
  it('needs every left matched to its right', () => {
    expect(match.check(q, ['1', '2', '3']).correct).toBe(true);
    expect(match.check(q, ['1', '3', '2']).correct).toBe(false);
  });
});

describe('reflect and task', () => {
  it('are never auto-graded as wrong', () => {
    expect(reflect.check({ ...base, id: 'q', type: 'reflect', model_answer: 'm', rubric: [] }, '').correct).toBe(true);
    expect(task.check({ ...base, id: 'q', type: 'task', instructions: 'i', done_means: 'd' }, false).correct).toBe(true);
  });
});

describe('Attempt (2 tries, hints, reveal)', () => {
  const q: TextQuestion = { ...base, id: 'q1', type: 'text', accept: ['x'], case_sensitive: false };

  it('correct first time with no hints = correct (1)', () => {
    const a = new Attempt(q);
    expect(a.submit(true)).toBe('correct');
    expect(a.result).toBe('correct');
    expect(a.done).toBe(true);
  });
  it('correct after a hint = correct-hinted (0.5)', () => {
    const a = new Attempt(q);
    expect(a.nextHint()).toBe('h1');
    expect(a.hintsLeft).toBe(1);
    a.submit(true);
    expect(a.result).toBe('correct-hinted');
  });
  it('correct on the second try = correct-hinted (0.5)', () => {
    const a = new Attempt(q);
    expect(a.submit(false)).toBe('not-yet');
    expect(a.done).toBe(false);
    expect(a.submit(true)).toBe('correct');
    expect(a.result).toBe('correct-hinted');
  });
  it('wrong twice = wrong (0), answer revealed', () => {
    const a = new Attempt(q);
    a.submit(false);
    expect(a.submit(false)).toBe('revealed');
    expect(a.result).toBe('wrong');
    expect(() => a.submit(true)).toThrow();
  });
  it('Show answer = wrong', () => {
    const a = new Attempt(q);
    a.reveal();
    expect(a.result).toBe('wrong');
  });
  it('at most as many hints as the question has', () => {
    const a = new Attempt(q);
    a.nextHint();
    a.nextHint();
    expect(a.nextHint()).toBeNull();
    expect(a.hintsShown).toBe(2);
  });
  it('self-rating maps to self-got / self-partly / self-not', () => {
    for (const [r, want] of [['got', 'self-got'], ['partly', 'self-partly'], ['not', 'self-not']] as const) {
      const a = new Attempt(q);
      a.selfRate(r);
      expect(a.result).toBe(want);
    }
  });
});

describe('scoreRun', () => {
  it('scores 1 / 0.5 / 0 and leaves tasks out', () => {
    expect(scoreRun(['correct', 'correct-hinted', 'wrong', 'task', 'self-got', 'self-partly', 'self-not'])).toEqual({ points: 3, total: 6, score: 0.5 });
  });
  it('a run of only tasks scores 0 out of 0', () => {
    expect(scoreRun(['task'])).toEqual({ points: 0, total: 0, score: 0 });
  });
});
