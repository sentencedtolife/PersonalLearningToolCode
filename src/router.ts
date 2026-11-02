// Tiny hash router: #/home, #/lesson/<id>, #/lesson/<id>/practice, #/ask?unit=C.loops ...

export interface View {
  title: string;
  el: HTMLElement;
  /** Context actions for the header bar (right side). */
  actions?: HTMLElement[];
  /** Return true if the key was handled. Called before global shortcuts. */
  onKey?(e: KeyboardEvent): boolean;
  /** Called after the view is in the DOM. */
  mounted?(): void;
  destroy?(): void;
}

export type ViewFactory = (params: Record<string, string>, query: URLSearchParams) => View | Promise<View>;

interface Route { parts: string[]; factory: ViewFactory }

const routes: Route[] = [];
let current: View | null = null;
let renderToken = 0;
const stack: string[] = [];

export function route(pattern: string, factory: ViewFactory): void {
  routes.push({ parts: pattern.split('/').filter(Boolean), factory });
}

export function currentView(): View | null {
  return current;
}

export function currentHash(): string {
  return location.hash || '#/home';
}

export function navigate(hash: string, opts: { replace?: boolean } = {}): void {
  if (hash === currentHash()) { void rerender(); return; }
  if (opts.replace) {
    if (stack.length) stack[stack.length - 1] = hash;
    location.replace(hash);
  } else {
    location.hash = hash;
  }
}

export function back(): void {
  if (stack.length > 1) history.back();
  else if (currentHash() !== '#/home') navigate('#/home', { replace: true });
}

function match(hash: string): { factory: ViewFactory; params: Record<string, string>; query: URLSearchParams } | null {
  const [path, qs = ''] = hash.replace(/^#/, '').split('?');
  const parts = path.split('/').filter(Boolean).map(decodeURIComponent);
  for (const r of routes) {
    const params: Record<string, string> = {};
    let ok = parts.length <= r.parts.length;
    for (let i = 0; ok && i < r.parts.length; i++) {
      const rp = r.parts[i];
      const p = parts[i];
      const optional = rp.endsWith('?');
      if (p === undefined) { ok = optional; break; }
      if (rp.startsWith(':')) params[rp.slice(1).replace('?', '')] = p;
      else if (rp.replace('?', '') !== p) ok = false;
    }
    if (ok) return { factory: r.factory, params, query: new URLSearchParams(qs) };
  }
  return null;
}

let container: HTMLElement;
let onRenderedCb: (hash: string, view: View) => void;

async function rerender(): Promise<void> {
  const hash = currentHash();
  if (stack.length > 1 && stack[stack.length - 2] === hash) stack.pop();
  else if (stack[stack.length - 1] !== hash) stack.push(hash);

  const token = ++renderToken;
  const m = match(hash) ?? match('#/home')!;
  let view: View;
  try {
    view = await m.factory(m.params, m.query);
  } catch (err) {
    console.error(err);
    const el = document.createElement('div');
    el.className = 'page';
    el.textContent = `Something went wrong opening this screen: ${(err as Error).message}`;
    view = { title: 'Error', el };
  }
  if (token !== renderToken) { view.destroy?.(); return; }
  current?.destroy?.();
  current = view;
  container.replaceChildren(view.el);
  container.scrollTop = 0;
  onRenderedCb(hash, view);
  view.mounted?.();
}

export function startRouter(el: HTMLElement, onRendered: (hash: string, view: View) => void): void {
  container = el;
  onRenderedCb = onRendered;
  window.addEventListener('hashchange', () => void rerender());
  void rerender();
}
