import type { ViewFactory } from '../router';
import { h } from '../ui/components';

export function placeholderView(title: string): ViewFactory {
  return () => ({ title, el: h('div', { class: 'page' }, h('h1', {}, title), h('p', { class: 'muted' }, 'This screen is not built yet.')) });
}
