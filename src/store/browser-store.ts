import type { Store } from './store';

const PREFIX = 'nordlys:/';

/** Used by `npm run dev` in a plain browser. localStorage writes are already atomic. */
export class BrowserStore implements Store {
  constructor(private storage: Storage) {}

  async readText(path: string): Promise<string> {
    const v = this.storage.getItem(PREFIX + path);
    if (v === null) throw new Error(`File not found: ${path}`);
    return v;
  }

  async writeText(path: string, text: string): Promise<void> {
    this.storage.setItem(PREFIX + path, text);
  }

  async list(dir: string): Promise<string[]> {
    const prefix = PREFIX + (dir ? `${dir}/` : '');
    const out: string[] = [];
    for (let i = 0; i < this.storage.length; i++) {
      const key = this.storage.key(i);
      if (key && key.startsWith(prefix)) {
        const rest = key.slice(prefix.length);
        if (!rest.includes('/')) out.push(rest);
      }
    }
    return out.sort();
  }

  async exists(path: string): Promise<boolean> {
    return this.storage.getItem(PREFIX + path) !== null;
  }

  async remove(path: string): Promise<void> {
    this.storage.removeItem(PREFIX + path);
  }
}
