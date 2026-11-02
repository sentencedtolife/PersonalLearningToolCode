// Shape of curriculum.json (HANDOFF section 12).

export interface Unit { id: string; title: string; tiers: number[]; prereqs: string[] }
export interface TrackInfo { code: string; name: string; share: number; units: Unit[] }
export interface Curriculum { version: number; tracks: TrackInfo[] }

export function parseCurriculum(text: string): Curriculum {
  const raw = JSON.parse(text) as Curriculum;
  if (!raw || !Array.isArray(raw.tracks)) throw new Error('curriculum.json has no "tracks" list');
  return raw;
}

export function allUnits(c: Curriculum): Unit[] {
  return c.tracks.flatMap((t) => t.units);
}

export function unitById(c: Curriculum, id: string): Unit | undefined {
  return allUnits(c).find((u) => u.id === id);
}

export function trackName(c: Curriculum, code: string): string {
  return c.tracks.find((t) => t.code === code)?.name ?? code;
}
