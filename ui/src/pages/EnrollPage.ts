import { renderAppNav, wireAppNav } from '../layout/appShell';

export function mountEnrollPage(root: HTMLElement): void {
  root.innerHTML = `
    <div class="app-shell">
      ${renderAppNav('enroll')}
      <main class="app-content">
        <div class="card">
          <h1 class="card-title">说话人注册</h1>
          <p class="card-subtitle page-placeholder">Phase 3 占位 — 将替换 EnrollmentDialog。</p>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'enroll');
}
