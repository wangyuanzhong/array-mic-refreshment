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
          <header class="page-hero">
            <h1 class="page-hero__title">欢迎开始使用</h1>
            <p class="page-hero__lead">
              版本 ${info.version} · 花一分钟确认环境，马上就能顺畅使用语音输入。
            </p>
          </header>

          <ul class="checklist">
            <li class="checklist__item">
              <span class="checklist__icon" aria-hidden="true">1</span>
              <span>确认麦克风已连接，并在 Windows「声音设置」中可用</span>
            </li>
            <li class="checklist__item">
              <span class="checklist__icon" aria-hidden="true">2</span>
              <span>在程序目录旁放置 <code>models/</code> 文件夹，或运行 <code>scripts\\download-models.ps1</code> 下载模型</span>
            </li>
            <li class="checklist__item">
              <span class="checklist__icon" aria-hidden="true">3</span>
              <span>可选：注册说话人，启用声纹校验，让识别更安心</span>
            </li>
            <li class="checklist__item">
              <span class="checklist__icon" aria-hidden="true">4</span>
              <span>在设置中配置 PTT 热键与 ASR 模型，按自己的习惯来</span>
            </li>
          </ul>

          <div class="page-actions">
            <button type="button" id="onboard-settings" class="btn-primary">打开设置</button>
            <button type="button" id="onboard-enroll" class="btn-ghost">注册说话人</button>
            <button type="button" id="onboard-skip" class="btn-ghost">稍后再说</button>
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
