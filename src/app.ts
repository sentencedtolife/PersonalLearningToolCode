// Shared app state: the data store and settings. Loaded once at boot.
import { createStore, type Store } from './store/store';
import { applySettings, parseSettings, type Settings } from './settings';

export const app = {
  store: null as unknown as Store,
  settings: null as unknown as Settings,
};

async function readOrNull(path: string): Promise<string | null> {
  try {
    return (await app.store.exists(path)) ? await app.store.readText(path) : null;
  } catch {
    return null;
  }
}

export async function loadApp(): Promise<void> {
  app.store = await createStore();
  app.settings = parseSettings(await readOrNull('settings.json'));
  applySettings(app.settings);
}

export async function saveSettings(patch: Partial<Settings> = {}): Promise<void> {
  Object.assign(app.settings, patch);
  applySettings(app.settings);
  await app.store.writeText('settings.json', JSON.stringify(app.settings, null, 2));
}
