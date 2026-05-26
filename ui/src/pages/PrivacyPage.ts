import { getBridge } from '../bridge';
import { escapeHtml, renderAppNav, wireAppNav } from '../layout/appShell';

function readQueryParam(name: string): string {
  const hash = window.location.hash;
  const queryStart = hash.indexOf('?');
  if (queryStart < 0) {
    return '';
  }

  const params = new URLSearchParams(hash.slice(queryStart + 1));
  return params.get(name) ?? '';
}

export async function mountPrivacyPage(root: HTMLElement): Promise<void> {
  const bridge = await getBridge();
  const apiBaseUrl = readQueryParam('apiBaseUrl');
  const state = await bridge.getPrivacyConsentState(apiBaseUrl);

  root.innerHTML = `
    <div class="app-shell">
      ${renderAppNav('privacy')}
      <main class="app-content">
        <div class="card">
          <h1 class="card-title">隐私确认</h1>
          <p class="card-subtitle">启用提示词整理前，请确认是否允许将识别文本发送到远程 API。</p>
          <p id="privacy-message" class="card-subtitle"></p>
          ${
            state.host
              ? `<p class="card-subtitle">目标主机：<strong>${escapeHtml(state.host)}</strong></p>`
              : ''
          }
          ${
            state.isLoopback
              ? '<p class="card-subtitle" style="color:var(--color-success)">本地 API（loopback）无需额外确认。</p>'
              : ''
          }
          <div style="display:flex;gap:var(--space-2);flex-wrap:wrap;margin-top:var(--space-4)">
            <button type="button" id="privacy-accept" class="btn-primary">同意并继续</button>
            <button type="button" id="privacy-decline" class="btn-ghost">取消</button>
          </div>
          <p id="privacy-status" class="card-subtitle" style="margin-top:var(--space-3)"></p>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'privacy');

  const messageEl = root.querySelector('#privacy-message')!;
  messageEl.textContent =
    state.message ||
    (state.host
      ? `提示词整理将把识别文本发送到 ${state.host}。继续？`
      : '无需隐私确认。');

  const acceptBtn = root.querySelector<HTMLButtonElement>('#privacy-accept')!;
  const declineBtn = root.querySelector<HTMLButtonElement>('#privacy-decline')!;
  const statusEl = root.querySelector('#privacy-status') as HTMLElement;

  if (!state.needsPrompt || state.isLoopback) {
    acceptBtn.textContent = '关闭';
    declineBtn.hidden = true;
  }

  acceptBtn.addEventListener('click', async () => {
    if (!state.needsPrompt || state.isLoopback) {
      await bridge.requestClose(true);
      return;
    }

    const result = await bridge.acceptPrivacy(state.host);
    if (!result.ok) {
      statusEl.textContent = '无法接受隐私条款，请重试。';
      statusEl.style.color = 'var(--color-danger)';
      return;
    }

    await bridge.requestClose(true);
  });

  declineBtn.addEventListener('click', async () => {
    await bridge.requestClose(false);
  });
}
