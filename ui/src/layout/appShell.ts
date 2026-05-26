import { navigate, type RouteId } from '../router';

export const NAV_ROUTES: { id: RouteId; label: string }[] = [
  { id: 'settings', label: '设置' },
  { id: 'enroll', label: '说话人注册' },
  { id: 'onboarding', label: '首次引导' },
  { id: 'privacy', label: '隐私' },
];

export function renderAppNav(active: RouteId): string {
  const links = NAV_ROUTES.map(
    ({ id, label }) =>
      `<a class="app-nav__link${id === active ? ' is-active' : ''}" href="#/${id}" data-route="${id}">${label}</a>`,
  ).join('');
  return `
    <nav class="app-nav" aria-label="主导航">
      <h1 class="app-nav__title">Array Mic</h1>
      <div class="app-nav__list">${links}</div>
    </nav>
  `;
}

export function wireAppNav(root: HTMLElement, active: RouteId): void {
  for (const link of root.querySelectorAll<HTMLAnchorElement>('.app-nav__link')) {
    link.addEventListener('click', (event) => {
      event.preventDefault();
      const route = link.dataset.route as RouteId | undefined;
      if (route && route !== active) {
        navigate(route);
      }
    });
  }
}

export function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
