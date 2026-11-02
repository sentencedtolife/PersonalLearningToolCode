// Lesson file text -> raw frontmatter + question blocks, with line numbers.
// Question blocks are found by splitting lines, not by markdown-it (see HANDOFF 5.1).
import { load, YAMLException } from 'js-yaml';
import type { Issue } from './types';

export interface RawBlock {
  /** Parsed YAML, or undefined if it failed to parse. */
  data: unknown;
  /** 1-based line of the opening ~~~question fence. */
  line: number;
  /** Top-level YAML key -> 1-based line in the file. */
  fieldLines: Record<string, number>;
  inPractice: boolean;
  /** Placeholder written into the body in place of the block. */
  placeholder: string;
}

export interface ParsedLesson {
  meta: Record<string, unknown> | null;
  metaLines: Record<string, number>;
  body: string;
  blocks: RawBlock[];
  hasPractice: boolean;
  /** Structural problems: missing frontmatter, YAML syntax errors, unclosed blocks. */
  issues: Issue[];
}

/** Removes a byte-order mark and converts Windows/old-Mac line endings to \n. */
export function normalizeText(text: string): string {
  return text.replace(/^﻿/, '').replace(/\r\n?/g, '\n');
}

/**
 * If the text contains an outer fence (a line of 4+ backticks, optionally followed by "lesson",
 * and a later line of the same number of backticks), keep only what is between them.
 * Everything outside is chat text.
 */
export function stripOuterFence(text: string): string {
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const open = /^(`{4,})\s*(?:lesson)?\s*$/.exec(lines[i]);
    if (!open) continue;
    const fence = open[1];
    for (let j = lines.length - 1; j > i; j--) {
      if (lines[j].trim() === fence) return lines.slice(i + 1, j).join('\n');
    }
    return text;
  }
  return text;
}

const KEY_LINE = /^([A-Za-z_][\w-]*)\s*:/;
export const PRACTICE_HEADING = /^##\s+Practice\s*$/i;

function yamlIssue(err: unknown, firstLine: number, field: string): Issue {
  const e = err as YAMLException;
  const line = e?.mark ? firstLine + e.mark.line : firstLine;
  const reason = (e?.reason || String(err)).replace(/\s+/g, ' ').trim();
  return {
    severity: 'error',
    line,
    field,
    message: `YAML syntax error: ${reason}.`,
    fix: 'Check indentation (2 spaces, no tabs) and put text that contains ": " or starts with a special character (` [ { * & ! | > \' " % @) in double quotes.',
  };
}

function keyLines(lines: string[], from: number, to: number): Record<string, number> {
  const out: Record<string, number> = {};
  for (let i = from; i < to; i++) {
    const m = KEY_LINE.exec(lines[i]);
    if (m && !(m[1] in out)) out[m[1]] = i + 1;
  }
  return out;
}

export function parseLesson(text: string): ParsedLesson {
  const lines = text.split('\n');
  const issues: Issue[] = [];
  let meta: Record<string, unknown> | null = null;
  let metaLines: Record<string, number> = {};
  let bodyStart = 0;

  if (lines[0]?.trim() !== '---') {
    issues.push({
      severity: 'error', line: 1, field: 'frontmatter',
      message: 'The file must start with a line containing only ---.',
      fix: 'Put the frontmatter (id, title, track, …) between two --- lines at the very top of the lesson.',
    });
  } else {
    let close = -1;
    for (let i = 1; i < lines.length; i++) {
      if (lines[i].trim() === '---') { close = i; break; }
    }
    if (close === -1) {
      issues.push({
        severity: 'error', line: 1, field: 'frontmatter',
        message: 'The frontmatter is never closed.',
        fix: 'Add a line containing only --- after the last frontmatter field.',
      });
    } else {
      metaLines = keyLines(lines, 1, close);
      try {
        const data = load(lines.slice(1, close).join('\n'));
        if (data && typeof data === 'object' && !Array.isArray(data)) meta = data as Record<string, unknown>;
        else issues.push({
          severity: 'error', line: 2, field: 'frontmatter',
          message: 'The frontmatter must be a list of "field: value" lines.',
          fix: 'Write each field on its own line, e.g. title: "Core Loops".',
        });
      } catch (err) {
        issues.push(yamlIssue(err, 2, 'frontmatter'));
      }
      bodyStart = close + 1;
    }
  }

  const bodyLines: string[] = [];
  const blocks: RawBlock[] = [];
  let inPractice = false;
  let hasPractice = false;
  for (let i = bodyStart; i < lines.length; i++) {
    const line = lines[i];
    if (PRACTICE_HEADING.test(line)) { inPractice = true; hasPractice = true; }
    if (line.trimEnd() !== '~~~question') { bodyLines.push(line); continue; }

    let close = -1;
    for (let k = i + 1; k < lines.length; k++) {
      if (lines[k].trimEnd() === '~~~') { close = k; break; }
    }
    if (close === -1) {
      issues.push({
        severity: 'error', line: i + 1, field: 'question',
        message: 'This question block is never closed.',
        fix: 'Add a line containing only ~~~ after the last field of the question.',
      });
      break;
    }
    let data: unknown;
    try {
      data = load(lines.slice(i + 1, close).join('\n'));
    } catch (err) {
      issues.push(yamlIssue(err, i + 2, `question at line ${i + 1}`));
    }
    const rawId = data && typeof data === 'object' ? (data as Record<string, unknown>).id : undefined;
    const placeholder = `<!--Q:${typeof rawId === 'string' || typeof rawId === 'number' ? rawId : `#${blocks.length}`}-->`;
    blocks.push({ data, line: i + 1, fieldLines: keyLines(lines, i + 1, close), inPractice, placeholder });
    bodyLines.push('', placeholder, '');
    i = close;
  }

  return { meta, metaLines, body: bodyLines.join('\n'), blocks, hasPractice, issues };
}
