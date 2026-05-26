import { getBridge } from '../bridge';
import { renderAppNav, wireAppNav } from '../layout/appShell';
import { navigate } from '../router';

/** First-run onboarding skeleton (Phase 3 optional). */
export async function mountOnboardingPage(root: HTMLElement): Promise<void> {
  const bridge = await getBridge();
  const info = await bridge.getAppInfo();

  root.innerHTML = `
    <div class="app-shell">
      ${renderAppNav('onboarding')}
      <main class="app-content">
        <div class="card">
          <h1 class="card-title">欢迎使用 Array Mic Refreshment</h1>
          <p class="card-subtitle">版本 ${info.version} — 快速检查您的环境是否就绪。</p>
          <ul class="card-subtitle" style="padding-left:1.25rem;line-height:1.8">
            <li>确认麦克风已连接并在 Windows 声音设置中可用</li>
            <li>在 exe 旁放置 <code>models/</code> 目录（或运行 <code>scripts\\download-models.ps1</code>）</li>
            <li>可选：注册说话人以启用声纹校验</li>
            <li>在设置中配置 PTT 热键与 ASR 模型</li>
          </ul>
          <div style="display:flex;gap:var(--space-2);flex-wrap:wrap;margin-top:var(--space-4)">
            <button type="button" id="onboard-settings" class="btn-primary">打开设置</button>
            <button type="button" id="onboard-enroll" class="btn-ghost">注册说话人</button>
            <button type="button" id="onboard-skip" class="btn-ghost">跳过</button>
          </div>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'onboarding');

  root.querySelector('#onboard-settings')!.addEventListener('click', () => navigate('settings'));
  root.querySelector('#onboard-enroll')!.addEventListener('click', () => navigate('enroll'));
  root.querySelector('#onboard-skip')!.addEventListener('click', async () => {
    await bridge.requestClose(true);
  });
}
