import type { Question, QuestionType } from '../../lesson/types';
import type { CheckResult, RenderCtx, TypeModule, Widget } from './common';
import { fill } from './fill';
import { match } from './match';
import { mcq } from './mcq';
import { multi } from './multi';
import { numeric } from './numeric';
import { order } from './order';
import { reflect } from './reflect';
import { task } from './task';
import { text } from './text';

// Each module is typed to its own question type; the map erases that.
const MODULES: Record<QuestionType, TypeModule<any, any>> = { mcq, multi, numeric, text, fill, order, match, reflect, task };

export function typeModule(q: Question): TypeModule<Question, unknown> {
  return MODULES[q.type];
}

export function checkAnswer(q: Question, answer: unknown): CheckResult {
  return MODULES[q.type].check(q, answer);
}

export function renderWidget(q: Question, ctx: RenderCtx): Widget<unknown> {
  return MODULES[q.type].render(q, ctx);
}
