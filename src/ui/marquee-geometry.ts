// Pure geometry for marquee resizing. All sizes are logical (CSS) pixels unless named *Phys.

export interface Size { w: number; h: number }
export interface Bounds { minW: number; minH: number; maxW: number; maxH: number }
export interface Rect { x: number; y: number; w: number; h: number }

export const MIN_SIZE: Size = { w: 720, h: 520 }; // same as minWidth/minHeight in tauri.conf.json
export const STEP = 16;
export const BIG_STEP = 96;

export function clampSize(w: number, h: number, b: Bounds): Size {
  return {
    w: Math.round(Math.min(Math.max(w, b.minW), Math.max(b.minW, b.maxW))),
    h: Math.round(Math.min(Math.max(h, b.minH), Math.max(b.minH, b.maxH))),
  };
}

/**
 * The window's top-left corner stays put; the bottom-right corner follows the pointer.
 * offX/offY keep the grab point where it was, so pressing the grip doesn't make the size jump.
 */
export function targetFromPointer(clientX: number, clientY: number, offX: number, offY: number, b: Bounds): Size {
  return clampSize(clientX + offX, clientY + offY, b);
}

/** Arrow keys: → / ← change the width, ↓ / ↑ the height. Shift = bigger steps. */
export function keyStep(size: Size, key: string, shift: boolean, b: Bounds): Size | null {
  const d = shift ? BIG_STEP : STEP;
  switch (key) {
    case 'ArrowRight': return clampSize(size.w + d, size.h, b);
    case 'ArrowLeft': return clampSize(size.w - d, size.h, b);
    case 'ArrowDown': return clampSize(size.w, size.h + d, b);
    case 'ArrowUp': return clampSize(size.w, size.h - d, b);
    default: return null;
  }
}

/**
 * The largest size that keeps the window inside its monitor's work area (the screen minus the taskbar).
 * Computed in physical pixels, then converted with the window's scale factor, so it is right on
 * high-DPI screens and when monitors use different scaling.
 */
export function boundsForWorkArea(windowPhys: { x: number; y: number }, workAreaPhys: Rect | null, scale: number, min: Size = MIN_SIZE): Bounds {
  if (!workAreaPhys) return { minW: min.w, minH: min.h, maxW: Infinity, maxH: Infinity };
  const maxW = (workAreaPhys.x + workAreaPhys.w - windowPhys.x) / scale;
  const maxH = (workAreaPhys.y + workAreaPhys.h - windowPhys.y) / scale;
  return { minW: min.w, minH: min.h, maxW: Math.max(min.w, Math.floor(maxW)), maxH: Math.max(min.h, Math.floor(maxH)) };
}

/** While dragging, the window grows to fit the preview but never shrinks below where it started. */
export function liveWindowSize(start: Size, target: Size): Size {
  return { w: Math.max(start.w, target.w), h: Math.max(start.h, target.h) };
}
