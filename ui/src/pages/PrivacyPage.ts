import { renderAppNav, wireAppNav } from '../layout/appShell';

export function mountPrivacyPage(root: HTMLElement): void {
  root.innerHTML = `
    <div class="app-shell">
      ${renderAppNav('privacy')}
      <main class="app-content">
        <div class="card">
          <h1 class="card-title">隐私</h1>
          <p class="card-subtitle page-placeholder">Phase 3 占位 — 将替换 PrivacyConsent。</p>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'privacy');
}
