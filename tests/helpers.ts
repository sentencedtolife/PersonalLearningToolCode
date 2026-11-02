import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export function readRepoFile(rel: string): string {
  return readFileSync(fileURLToPath(new URL(`../${rel}`, import.meta.url)), 'utf8');
}

export function bundledLessonFiles(): string[] {
  return readdirSync(fileURLToPath(new URL('../content/lessons/', import.meta.url))).filter((f) => f.endsWith('.lesson.md'));
}

export function knownUnits(): Set<string> {
  const cur = JSON.parse(readRepoFile('content/curriculum.json')) as { tracks: { units: { id: string }[] }[] };
  return new Set(cur.tracks.flatMap((t) => t.units.map((u) => u.id)));
}
