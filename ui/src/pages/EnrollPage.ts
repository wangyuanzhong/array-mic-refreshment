import { getBridge } from '../bridge';
import { escapeHtml, renderAppNav, wireAppNav } from '../layout/appShell';
import { navigate } from '../router';

/** Matches EnrollmentDialog phrase list (min 3 of 5 required). */
const ENROLLMENT_PHRASES = [
  '请说：今天天气很好，适合出去散步',
  '请说：打开代码编辑器，开始编写程序',
  '请说：我要写一封邮件，通知大家开会',
  '请说：语音识别技术正在快速发展',
  '请说：人工智能改变了很多行业的工作方式',
] as const;

const MIN_UTTERANCES = 3;

export async function mountEnrollPage(root: HTMLElement): Promise<void> {
  const bridge = await getBridge();

  let phraseIndex = 0;
  let utteranceCount = 0;
  let recording = false;
  let statusText = '';
  let statusKind: 'error' | 'success' | '' = '';

  root.innerHTML = `
    <div class="app-shell">
      ${renderAppNav('enroll')}
      <main class="app-content">
        <div class="card">
          <h1 class="card-title">注册说话人</h1>
          <p class="card-subtitle">录入至少 ${MIN_UTTERANCES} 段语音以建立声纹档案（建议完成全部 ${ENROLLMENT_PHRASES.length} 段）。</p>
          <label for="enroll-name">姓名</label>
          <input id="enroll-name" type="text" placeholder="输入您的姓名" autocomplete="name" style="width:100%;margin-bottom:var(--space-4)" />
          <div id="enroll-progress" class="page-placeholder" style="font-family:ui-monospace,monospace;letter-spacing:0.15em"></div>
          <p id="enroll-phrase" class="card-subtitle"></p>
          <div style="display:flex;gap:var(--space-2);flex-wrap:wrap">
            <button type="button" id="enroll-record" class="btn-primary">开始录音</button>
            <button type="button" id="enroll-finish" class="btn-ghost" disabled>完成注册</button>
          </div>
          <p id="enroll-status" class="card-subtitle" style="margin-top:var(--space-3)"></p>
        </div>
      </main>
    </div>
  `;
  wireAppNav(root, 'enroll');

  const nameInput = root.querySelector<HTMLInputElement>('#enroll-name')!;
  const progressEl = root.querySelector('#enroll-progress')!;
  const phraseEl = root.querySelector('#enroll-phrase')!;
  const recordBtn = root.querySelector<HTMLButtonElement>('#enroll-record')!;
  const finishBtn = root.querySelector<HTMLButtonElement>('#enroll-finish')!;
  const statusEl = root.querySelector('#enroll-status') as HTMLElement;

  const updateUi = () => {
    progressEl.textContent = ENROLLMENT_PHRASES.map((_, i) => {
      if (i < utteranceCount) return '✓';
      if (i === phraseIndex && phraseIndex < ENROLLMENT_PHRASES.length) return '●';
      return '○';
    }).join(' ');

    if (phraseIndex >= ENROLLMENT_PHRASES.length) {
      phraseEl.textContent = '全部完成，可点击「完成注册」。';
      recordBtn.disabled = true;
    } else {
      phraseEl.textContent = `第 ${phraseIndex + 1}/${ENROLLMENT_PHRASES.length} 段：${ENROLLMENT_PHRASES[phraseIndex]}`;
      recordBtn.disabled = false;
    }

    recordBtn.textContent = recording ? '停止' : '开始录音';
    const name = nameInput.value.trim();
    finishBtn.disabled = utteranceCount < MIN_UTTERANCES || !name;

    statusEl.textContent = statusText;
    statusEl.style.color =
      statusKind === 'error'
        ? 'var(--color-danger)'
        : statusKind === 'success'
          ? 'var(--color-success)'
          : 'var(--color-text-muted)';
  };

  nameInput.addEventListener('input', updateUi);

  recordBtn.addEventListener('click', async () => {
    statusText = '';
    statusKind = '';

    if (!recording) {
      const start = await bridge.startEnrollmentUtterance();
      if (!start.ok) {
        statusText = start.error ?? '无法开始录音';
        statusKind = 'error';
        updateUi();
        return;
      }
      recording = true;
      updateUi();
      return;
    }

    recording = false;
    const result = await bridge.stopEnrollmentUtterance();
    utteranceCount = result.utteranceCount;
    phraseIndex = Math.min(utteranceCount, ENROLLMENT_PHRASES.length);

    if (!result.ok) {
      statusText = result.message ?? '录音无效，请重试';
      statusKind = 'error';
      updateUi();
      return;
    }

    if (result.durationMs < 1000) {
      statusText = '录音太短，请录 3~5 秒。';
      statusKind = 'error';
      updateUi();
      return;
    }

    if (result.durationMs > 10000) {
      statusText = '录音较长，已接受本段。';
    }

    updateUi();
  });

  finishBtn.addEventListener('click', async () => {
    const name = nameInput.value.trim();
    if (!name) {
      statusText = '请输入姓名。';
      statusKind = 'error';
      updateUi();
      return;
    }

    if (utteranceCount < MIN_UTTERANCES) {
      statusText = `请至少完成 ${MIN_UTTERANCES} 段录音。`;
      statusKind = 'error';
      updateUi();
      return;
    }

    finishBtn.disabled = true;
    const result = await bridge.completeEnrollment(name, utteranceCount);
    if (!result.ok) {
      statusText = result.error ?? '注册失败';
      statusKind = 'error';
      finishBtn.disabled = false;
      updateUi();
      return;
    }

    statusText = '说话人注册成功！';
    statusKind = 'success';
    updateUi();
    await bridge.requestClose(true);
  });

  updateUi();
}
