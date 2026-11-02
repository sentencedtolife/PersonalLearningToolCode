/** All file access goes through this interface. Paths are relative to the data directory, using "/". */
export interface Store {
  readText(path: string): Promise<string>;
  /** Atomic where the backend allows it (temp file + rename). Creates parent folders. */
  writeText(path: string, text: string): Promise<void>;
  /** File names (not folders) directly inside `dir`. Empty if `dir` does not exist. */
  list(dir: string): Promise<string[]>;
  exists(path: string): Promise<boolean>;
  remove(path: string): Promise<void>;
}

export const isTauri = typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window;

export async function createStore(): Promise<Store> {
  if (isTauri) {
    const { TauriStore } = await import('./tauri-store');
    return new TauriStore();
  }
  const { BrowserStore } = await import('./browser-store');
  return new BrowserStore(window.localStorage);
}
