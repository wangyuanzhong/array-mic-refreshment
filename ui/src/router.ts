import { mountEnrollPage } from './pages/EnrollPage';
import { mountOnboardingPage } from './pages/OnboardingPage';
import { mountPrivacyPage } from './pages/PrivacyPage';
import { mountSettingsPage } from './pages/SettingsPage';

export type RouteId = 'settings' | 'enroll' | 'onboarding' | 'privacy';

type RouteHandler = (root: HTMLElement) => void | Promise<void>;

const routes: Record<RouteId, RouteHandler> = {
  settings: mountSettingsPage,
  enroll: mountEnrollPage,
  onboarding: mountOnboardingPage,
  privacy: mountPrivacyPage,
};

const DEFAULT_ROUTE: RouteId = 'settings';

function normalizeHash(hash: string): RouteId {
  const path = hash.replace(/^#\/?/, '').split('?')[0].toLowerCase();
  if (path in routes) {
    return path as RouteId;
  }
  return DEFAULT_ROUTE;
}

export function navigate(route: RouteId, replace = false): void {
  const next = `#/${route}`;
  if (replace) {
    window.location.replace(next);
  } else {
    window.location.hash = next;
  }
}

export function startRouter(root: HTMLElement): void {
  const render = async () => {
    const route = normalizeHash(window.location.hash);
    root.innerHTML = '';
    root.dataset.route = route;
    await routes[route](root);
  };

  window.addEventListener('hashchange', () => {
    void render();
  });

  if (!window.location.hash) {
    navigate(DEFAULT_ROUTE, true);
  } else {
    void render();
  }
}
