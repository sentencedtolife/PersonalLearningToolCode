import { BaseDirectory, exists, mkdir, readDir, readTextFile, remove, rename, writeTextFile } from '@tauri-apps/plugin-fs';
import type { Store } from './store';

const base = { baseDir: BaseDirectory.AppData };

function parentOf(path: string): string {
  const i = path.lastIndexOf('/');
  return i === -1 ? '' : path.slice(0, i);
}

/** Files live in the OS app-data folder, e.g. %APPDATA%\app.nordlys on Windows. */
export class TauriStore implements Store {
  readText(path: string): Promise<string> {
    return readTextFile(path, base);
  }

  async writeText(path: string, text: string): Promise<void> {
    const dir = parentOf(path);
    if (!(await exists(dir, base))) await mkdir(dir, { ...base, recursive: true });
    const tmp = `${path}.tmp`;
    await writeTextFile(tmp, text, base);
    await rename(tmp, path, { oldPathBaseDir: BaseDirectory.AppData, newPathBaseDir: BaseDirectory.AppData });
  }

  async list(dir: string): Promise<string[]> {
    if (!(await exists(dir, base))) return [];
    const entries = await readDir(dir, base);
    return entries.filter((e) => e.isFile).map((e) => e.name);
  }

  exists(path: string): Promise<boolean> {
    return exists(path, base);
  }

  remove(path: string): Promise<void> {
    return remove(path, base);
  }
}
