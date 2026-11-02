// Small wrappers so views never care whether they run in Tauri or a plain browser.
import { isTauri } from './store/store';

export async function copyText(text: string): Promise<void> {
  if (isTauri) {
    const { writeText } = await import('@tauri-apps/plugin-clipboard-manager');
    await writeText(text);
  } else {
    await navigator.clipboard.writeText(text);
  }
}

/** Returns false when there is no folder to open (browser mode). */
export async function openDataFolder(): Promise<boolean> {
  if (!isTauri) return false;
  const { appDataDir } = await import('@tauri-apps/api/path');
  const { openPath } = await import('@tauri-apps/plugin-opener');
  await openPath(await appDataDir());
  return true;
}

/** Asks where to save, then writes the file. Returns the chosen path, or null if cancelled. */
export async function saveFileAs(defaultName: string, text: string): Promise<string | null> {
  if (isTauri) {
    const { save } = await import('@tauri-apps/plugin-dialog');
    const { writeTextFile } = await import('@tauri-apps/plugin-fs');
    const path = await save({ defaultPath: defaultName, filters: [{ name: 'JSON', extensions: ['json'] }] });
    if (!path) return null;
    await writeTextFile(path, text);
    return path;
  }
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = defaultName;
  a.click();
  URL.revokeObjectURL(url);
  return defaultName;
}

export interface WindowControls {
  minimize(): void;
  toggleMaximize(): void;
  close(): void;
  isMaximized(): Promise<boolean>;
  onResized(cb: () => void): void;
}

export async function getWindowControls(): Promise<WindowControls | null> {
  if (!isTauri) return null;
  const { getCurrentWindow } = await import('@tauri-apps/api/window');
  const w = getCurrentWindow();
  return {
    minimize: () => void w.minimize(),
    toggleMaximize: () => void w.toggleMaximize(),
    close: () => void w.close(),
    isMaximized: () => w.isMaximized(),
    onResized: (cb) => void w.onResized(cb),
  };
}
