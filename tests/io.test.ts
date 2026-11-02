// Counts Store operations, so the cost of saving is measured rather than assumed.
import { describe, expect, it } from 'vitest';
import { app, saveProgress as appSave } from '../src/app';
import { emptyProgress, saveProgress } from '../src/progress/progress';
import { BrowserStore } from '../src/store/browser-store';
import type { Store } from '../src/store/store';

class MemoryStorage implements Storage {
  private m = new Map<string, string>();
  get length() { return this.m.size; }
  clear() { this.m.clear(); }
  getItem(k: string) { return this.m.get(k) ?? null; }
  key(i: number) { return [...this.m.keys()][i] ?? null; }
  removeItem(k: string) { this.m.delete(k); }
  setItem(k: string, v: string) { this.m.set(k, v); }
}

function countingStore(): { store: Store; ops: string[] } {
  const inner = new BrowserStore(new MemoryStorage());
  const ops: string[] = [];
  const store: Store = {
    readText: (p) => { ops.push(`read ${p}`); return inner.readText(p); },
    writeText: (p, t) => { ops.push(`write ${p}`); return inner.writeText(p, t); },
    list: (d) => { ops.push(`list ${d}`); return inner.list(d); },
    exists: (p) => { ops.push(`exists ${p}`); return inner.exists(p); },
    remove: (p) => { ops.push(`remove ${p}`); return inner.remove(p); },
  };
  return { store, ops };
}

describe('progress save cost', () => {
  it('checks for the daily backup once per day, then only writes', async () => {
    const { store, ops } = countingStore();
    const p = emptyProgress();
    await saveProgress(store, p, '2026-11-02');
    await saveProgress(store, p, '2026-11-02');
    ops.length = 0;
    await saveProgress(store, p, '2026-11-02');
    expect(ops).toEqual(['write progress.json']);
    // A new day checks again and makes that day's backup.
    ops.length = 0;
    await saveProgress(store, p, '2026-11-03');
    expect(ops).toContain('write backups/progress-2026-11-03.json');
  });

  it('a burst of saves in the same tick becomes one write', async () => {
    const { store, ops } = countingStore();
    app.store = store;
    app.progress = emptyProgress();
    await appSave(); // first save of the day does the backup check
    ops.length = 0;
    app.progress.resume.route = '#/a';
    const all = [appSave(), appSave(), appSave(), appSave(), appSave()];
    await Promise.all(all);
    expect(ops.filter((o) => o === 'write progress.json')).toHaveLength(1);
    expect(JSON.parse(await store.readText('progress.json')).resume.route).toBe('#/a');
  });

  it('a save requested during a write is not lost', async () => {
    const { store } = countingStore();
    app.store = store;
    app.progress = emptyProgress();
    const first = appSave();
    await Promise.resolve();
    app.progress.resume.route = '#/late';
    const second = appSave();
    await Promise.all([first, second]);
    expect(JSON.parse(await store.readText('progress.json')).resume.route).toBe('#/late');
  });
});
