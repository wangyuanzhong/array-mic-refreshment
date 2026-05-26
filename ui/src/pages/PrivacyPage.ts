import { getBridge } from '../bridge';
import { escapeHtml, renderAppNav, wireAppNav } from '../layout/appShell';

function readQueryParam(name: string): string {
  const hash = window.location.hash;
  const queryStart = hash.indexOf('?');
  if (queryStart < 0) {
    return '';
  }

  return new URLSearchParams(hash.slice(queryStart + 1)).get(name) ?? '';
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
          <header class="page-hero">
            <h1 class="page-hero__title">隐私与数据使用</h1>
            <p class="page-hero__lead">
              启用「提示词整理」时，识别出的文字可能会发送到远程 API 进行处理。
              请确认您了解并同意后再继续。
            </p>
          </header>

          <div id="privacy-message" class="info-callout"></div>
          ${
            state.host
              ? `<p class="form-hint">目标主机</p><p class="host-badge">${escapeHtml(state.host)}</p>`
              : ''
          }
          ${
            state.isLoopback
              ? '<p class="info-callout info-callout--success">检测到本地 API（loopback），数据不会离开本机，无需额外确认。</p>'
              : ''
          }

          <div class="page-actions">
            <button type="button" id="privacy-accept" class="btn-primary">我同意，继续</button>
            <button type="button" id="privacy-decline" class="btn-ghost">暂不启用</button>
          </div>
          <p id="privacy-status" class="status-message"></p>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'privacy');

  const messageEl = root.querySelector('#privacy-message')!;
  messageEl.textContent =
    state.message ||
    (state.host
      ? `提示词整理将把识别文本发送到 ${state.host}。是否继续？`
      : '当前无需隐私确认，可以直接使用。');

  const acceptBtn = root.querySelector<HTMLButtonElement>('#privacy-accept')!;
  const declineBtn = root.querySelector<HTMLButtonElement>('#privacy-decline')!;
  const statusEl = root.querySelector('#privacy-status') as HTMLElement;

  if (!state.needsPrompt || state.isLoopback) {
    acceptBtn.textContent = '知道了';
    declineBtn.hidden = true;
  }

  acceptBtn.addEventListener('click', async () => {
    if (!state.needsPrompt || state.isLoopback) {
      await bridge.requestClose(true);
      return;
    }

    const result = await bridge.acceptPrivacy(state.host);
    if (!result.ok) {
      statusEl.textContent = '暂时无法保存您的选择，请重试。';
      statusEl.className = 'status-message status-message--error';
      return;
    }

    await bridge.requestClose(true);
  });

  declineBtn.addEventListener('click', async () => {
    await bridge.requestClose(false);
  });
}
