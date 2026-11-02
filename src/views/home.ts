import type { ViewFactory } from '../router';
import { button, h } from '../ui/components';

export const homeView: ViewFactory = () => {
  const el = h('div', { class: 'home' },
    button('Next', () => {}, { variant: 'primary', big: true }));
  return { title: 'Nordlys', el };
};
