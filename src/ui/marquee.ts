// Marquee resizing: drag the grip in the bottom-right corner (or press Alt+F8 / Enter on the grip and use the
// arrow keys). A translucent blue rectangle previews the new window size; release or Enter applies it,
// Esc cancels. Normal edge resizing, Aero Snap and Win+arrow keys keep working alongside it.
import { getWindowControls } from '../platform';
import { h } from './components';
import { boundsForWorkArea, keyStep, liveWindowSize, targetFromPointer, type Bounds, type Size } from './marquee-geometry';

const GRIP_ICON = '<svg viewBox="0 0 12 12" aria-hidden="true"><path d="M11 3L3 11M11 7L7 11" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';

interface Session {
  mode: 'pointer' | 'keyboard';
  start: Size;
  target: Size;
  bounds: Bounds;
  offX: number;
  offY: number;
}

let startMarquee: ((mode: 'keyboard') => void) | null = null;

/** Alt+F8 (the KDE/Windows "resize window" key) starts keyboard resizing. */
export function startKeyboardResize(): boolean {
  if (!startMarquee) return false;
  startMarquee('keyboard');
  return true;
}

export async function initMarquee(): Promise<void> {
  const win = await getWindowControls();
  if (!win) return; // browser version: the browser owns the window

  const grip = h('button', {
    class: 'resize-grip', type: 'button', html: GRIP_ICON,
    title: 'Drag to resize the window. Keyboard: Enter, then arrow keys.',
    'aria-label': 'Resize window. Press Enter, then use the arrow keys.',
  });
  const rect = h('div', { class: 'marquee-rect' });
  const label = h('div', { class: 'marquee-label' });
  const live = h('div', { class: 'sr-only', 'aria-live': 'assertive' });
  const layer = h('div', { class: 'marquee-layer hidden' }, rect, label);
  document.body.append(grip, layer, live);

  let s: Session | null = null;
  let pendingPointer: { x: number; y: number; id: number } | null = null;
  let growInFlight = false;
  let growQueued = false;

  const draw = () => {
    if (!s) return;
    rect.style.width = `${s.target.w}px`;
    rect.style.height = `${s.target.h}px`;
    label.textContent = `${s.target.w} × ${s.target.h}`;
    label.style.left = `${Math.max(8, s.target.w - label.offsetWidth - 8)}px`;
    label.style.top = `${Math.max(8, s.target.h - label.offsetHeight - 8)}px`;
  };

  // The window grows live so the preview is visible beyond the old edge. One resize call at a time.
  const grow = async () => {
    if (!s) return;
    if (growInFlight) { growQueued = true; return; }
    growInFlight = true;
    try {
      const want = liveWindowSize(s.start, s.target);
      if (want.w !== Math.round(window.innerWidth) || want.h !== Math.round(window.innerHeight)) await win.setSize(want.w, want.h);
    } finally {
      growInFlight = false;
      if (growQueued) { growQueued = false; requestAnimationFrame(() => void grow()); }
    }
  };

  const end = () => {
    s = null;
    layer.classList.add('hidden');
    document.documentElement.classList.remove('marquee-active');
  };

  const apply = async () => {
    if (!s) return;
    const target = s.target;
    end();
    await win.setSize(target.w, target.h);
    live.textContent = `Window resized to ${target.w} by ${target.h}.`;
  };

  const cancel = async () => {
    if (!s) return;
    const start = s.start;
    end();
    await win.setSize(start.w, start.h);
    live.textContent = 'Resize cancelled.';
  };

  const begin = async (mode: Session['mode'], pointer?: { x: number; y: number }) => {
    if (s) return;
    if (await win.isFullscreen()) await win.setFullscreen(false);
    if (await win.isMaximized()) await win.unmaximize();
    const g = await win.geometry();
    const start = { w: Math.round(g.width), h: Math.round(g.height) };
    s = {
      mode,
      start,
      target: start,
      bounds: boundsForWorkArea({ x: g.xPhys, y: g.yPhys }, g.workAreaPhys, g.scale),
      offX: pointer ? start.w - pointer.x : 0,
      offY: pointer ? start.h - pointer.y : 0,
    };
    document.documentElement.classList.add('marquee-active');
    layer.classList.remove('hidden');
    draw();
    if (mode === 'keyboard') {
      live.textContent = 'Resize mode. Arrow keys change the size, Shift for bigger steps. Enter applies, Escape cancels.';
    } else if (pendingPointer) {
      // Catch up with movement that happened while the window size was being read.
      s.target = targetFromPointer(pendingPointer.x, pendingPointer.y, s.offX, s.offY, s.bounds);
      draw();
    }
  };
  startMarquee = (mode) => void begin(mode);

  grip.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return;
    e.preventDefault(); // no text selection, no focus jump
    grip.setPointerCapture(e.pointerId);
    pendingPointer = { x: e.clientX, y: e.clientY, id: e.pointerId };
    void begin('pointer', { x: e.clientX, y: e.clientY });
  });
  grip.addEventListener('pointermove', (e) => {
    if (!pendingPointer || e.pointerId !== pendingPointer.id) return;
    pendingPointer = { x: e.clientX, y: e.clientY, id: e.pointerId };
    if (!s) return;
    s.target = targetFromPointer(e.clientX, e.clientY, s.offX, s.offY, s.bounds);
    draw();
    void grow();
  });
  const release = (e: PointerEvent) => {
    if (!pendingPointer || e.pointerId !== pendingPointer.id) return;
    pendingPointer = null;
    if (s?.mode === 'pointer') void apply();
  };
  grip.addEventListener('pointerup', release);
  grip.addEventListener('lostpointercapture', release);
  grip.addEventListener('keydown', (e) => {
    if ((e.key === 'Enter' || e.key === ' ') && !s) { e.preventDefault(); e.stopPropagation(); void begin('keyboard'); }
  });

  // While resizing, these keys belong to the marquee (capture phase: before the app's own shortcuts).
  window.addEventListener('keydown', (e) => {
    if (!s) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    if (e.key === 'Escape') { pendingPointer = null; void cancel(); return; }
    if (s.mode === 'keyboard') {
      if (e.key === 'Enter') { void apply(); return; }
      const next = keyStep(s.target, e.key, e.shiftKey, s.bounds);
      if (next) { s.target = next; draw(); void grow(); }
    }
  }, true);

  // Keep the grip out of the way in fullscreen.
  const syncGrip = async () => grip.classList.toggle('hidden', await win.isFullscreen() || await win.isMaximized());
  win.onResized(() => { if (!s) void syncGrip(); });
  void syncGrip();
}

