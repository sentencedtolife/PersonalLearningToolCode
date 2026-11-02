import { describe, expect, it } from 'vitest';
import { boundsForWorkArea, clampSize, keyStep, liveWindowSize, targetFromPointer } from '../src/ui/marquee-geometry';

const b = { minW: 720, minH: 520, maxW: 1900, maxH: 1000 };

describe('marquee geometry', () => {
  it('clamps to the minimum and maximum size and rounds', () => {
    expect(clampSize(100, 100, b)).toEqual({ w: 720, h: 520 });
    expect(clampSize(5000, 5000, b)).toEqual({ w: 1900, h: 1000 });
    expect(clampSize(1000.6, 700.4, b)).toEqual({ w: 1001, h: 700 });
  });

  it('keeps the grab offset so pressing the grip does not jump', () => {
    // Window 1100x760, grip grabbed 6px inside the corner.
    const offX = 1100 - 1094;
    const offY = 760 - 754;
    expect(targetFromPointer(1094, 754, offX, offY, b)).toEqual({ w: 1100, h: 760 });
    expect(targetFromPointer(1294, 854, offX, offY, b)).toEqual({ w: 1300, h: 860 });
  });

  it('arrow keys resize in steps, Shift for big steps, within bounds', () => {
    expect(keyStep({ w: 1000, h: 700 }, 'ArrowRight', false, b)).toEqual({ w: 1016, h: 700 });
    expect(keyStep({ w: 1000, h: 700 }, 'ArrowUp', true, b)).toEqual({ w: 1000, h: 604 });
    expect(keyStep({ w: 730, h: 700 }, 'ArrowLeft', true, b)).toEqual({ w: 720, h: 700 });
    expect(keyStep({ w: 1000, h: 700 }, 'Enter', false, b)).toBeNull();
  });

  it('work-area bounds at 100% scaling', () => {
    // 1920x1040 work area (taskbar 40px), window at (100, 50).
    expect(boundsForWorkArea({ x: 100, y: 50 }, { x: 0, y: 0, w: 1920, h: 1040 }, 1)).toMatchObject({ maxW: 1820, maxH: 990 });
  });

  it('work-area bounds at 150% scaling use logical pixels', () => {
    // 3840x2100 physical work area, window at physical (300, 150), scale 1.5.
    expect(boundsForWorkArea({ x: 300, y: 150 }, { x: 0, y: 0, w: 3840, h: 2100 }, 1.5)).toMatchObject({ maxW: 2360, maxH: 1300 });
  });

  it('work-area bounds on a second monitor to the right', () => {
    expect(boundsForWorkArea({ x: 2020, y: 0 }, { x: 1920, y: 0, w: 2560, h: 1400 }, 1)).toMatchObject({ maxW: 2460, maxH: 1400 });
  });

  it('never offers less than the minimum, even near the screen edge', () => {
    expect(boundsForWorkArea({ x: 1800, y: 900 }, { x: 0, y: 0, w: 1920, h: 1040 }, 1)).toMatchObject({ maxW: 720, maxH: 520 });
  });

  it('the live window grows with the preview but not below the start size', () => {
    expect(liveWindowSize({ w: 1100, h: 760 }, { w: 1300, h: 700 })).toEqual({ w: 1300, h: 760 });
    expect(liveWindowSize({ w: 1100, h: 760 }, { w: 800, h: 600 })).toEqual({ w: 1100, h: 760 });
  });
});
