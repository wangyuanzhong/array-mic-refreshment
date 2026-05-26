import { renderAppNav, wireAppNav } from '../layout/appShell';

export function mountOnboardingPage(root: HTMLElement): void {
  root.innerHTML = `
    <div class="app-shell">
      ${renderAppNav('onboarding')}
      <main class="app-content">
        <div class="card">
          <h1 class="card-title">首次引导</h1>
          <p class="card-subtitle page-placeholder">Phase 3 占位 — 首次启动检测模型与麦克风。</p>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'onboarding');
}
