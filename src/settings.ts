export interface Settings {
  version: 1;
  theme: 'dark' | 'light';
  paperFont: 'sans' | 'serif';
  fontSize: 'S' | 'M' | 'L';
  /** true = no animations at all. The OS "reduce motion" preference also turns them off. */
  reduceMotion: boolean;
  sidebarCollapsed: boolean;
  lastEnergy: 1 | 2 | 3;
}

export const DEFAULT_SETTINGS: Settings = {
  version: 1,
  theme: 'dark',
  paperFont: 'sans',
  fontSize: 'M',
  reduceMotion: false,
  sidebarCollapsed: false,
  lastEnergy: 2,
};

export function parseSettings(text: string | null): Settings {
  if (!text) return { ...DEFAULT_SETTINGS };
  try {
    const raw = JSON.parse(text);
    return { ...DEFAULT_SETTINGS, ...(typeof raw === 'object' && raw ? raw : {}) };
  } catch {
    return { ...DEFAULT_SETTINGS };
  }
}

export function applySettings(s: Settings): void {
  const d = document.documentElement.dataset;
  d.theme = s.theme;
  d.paperFont = s.paperFont;
  d.fontSize = s.fontSize;
  d.motion = s.reduceMotion ? 'off' : 'on';
}
