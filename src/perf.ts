// Tiny startup/navigation timing log. Bounded, so long sessions never grow it.
// Read it from the dev tools console as window.__nordlysPerf.

const MAX_ROUTES = 50;

export const perf = {
  /** ms since navigation start, recorded at boot milestones. */
  boot: {} as Record<string, number>,
  /** Most recent route renders: hash and ms from hashchange to view in the DOM. */
  routes: [] as { hash: string; ms: number }[],
};

export function markBoot(name: string): void {
  perf.boot[name] = Math.round(performance.now() * 10) / 10;
}

export function recordRoute(hash: string, ms: number): void {
  perf.routes.push({ hash, ms: Math.round(ms * 10) / 10 });
  if (perf.routes.length > MAX_ROUTES) perf.routes.shift();
}

(globalThis as { __nordlysPerf?: typeof perf }).__nordlysPerf = perf;
