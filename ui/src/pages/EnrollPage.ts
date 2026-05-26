import { getBridge } from '../bridge';
import { renderAppNav, wireAppNav } from '../layout/appShell';

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
          <header class="page-hero">
            <h1 class="page-hero__title">建立您的声纹档案</h1>
            <p class="page-hero__lead">
              朗读几段示例句子，帮助我们认识您的声音。至少完成 ${MIN_UTTERANCES} 段即可开始；
              全部 ${ENROLLMENT_PHRASES.length} 段完成后识别会更准确。
            </p>
          </header>

          <div class="form-field page-section">
            <label for="enroll-name">怎么称呼您？</label>
            <input id="enroll-name" type="text" placeholder="请输入姓名" autocomplete="name" />
          </div>

          <div class="page-section">
            <p class="form-hint">录音进度</p>
            <div
              id="enroll-progress"
              class="progress-dots"
              role="progressbar"
              aria-valuemin="0"
              aria-valuemax="${ENROLLMENT_PHRASES.length}"
              aria-label="声纹注册进度"
            ></div>
          </div>

          <div id="enroll-phrase" class="phrase-card"></div>

          <div class="page-actions">
            <button type="button" id="enroll-record" class="btn-primary">开始录音</button>
            <button type="button" id="enroll-finish" class="btn-ghost" disabled>完成注册</button>
          </div>
          <p id="enroll-status" class="status-message"></p>
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
    progressEl.innerHTML = ENROLLMENT_PHRASES.map((_, i) => {
      let cls = 'progress-dot';
      if (i < utteranceCount) cls += ' progress-dot--done';
      else if (i === phraseIndex && phraseIndex < ENROLLMENT_PHRASES.length) cls += ' progress-dot--current';
      return `<span class="${cls}" title="第 ${i + 1} 段"></span>`;
    }).join('');
    progressEl.setAttribute('aria-valuenow', String(utteranceCount));

    if (phraseIndex >= ENROLLMENT_PHRASES.length) {
      phraseEl.innerHTML =
        '<span class="phrase-card__label">全部完成</span>太棒了！可以点「完成注册」保存您的声纹档案。';
      recordBtn.disabled = true;
    } else {
      phraseEl.innerHTML = `<span class="phrase-card__label">第 ${phraseIndex + 1} / ${ENROLLMENT_PHRASES.length} 段 · 请朗读</span>${ENROLLMENT_PHRASES[phraseIndex]}`;
      recordBtn.disabled = false;
    }

    recordBtn.textContent = recording ? '停止录音' : '开始录音';
    finishBtn.disabled = utteranceCount < MIN_UTTERANCES || !nameInput.value.trim();

    statusEl.textContent = statusText;
    statusEl.className =
      'status-message' + (statusKind ? ` status-message--${statusKind}` : '');
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
      statusText = result.message ?? '这段录音不太理想，请再试一次';
      statusKind = 'error';
      updateUi();
      return;
    }

    if (result.durationMs < 1000) {
      statusText = '录音太短啦，请录 3～5 秒左右。';
      statusKind = 'error';
      updateUi();
      return;
    }

    if (result.durationMs > 10000) {
      statusText = '录音稍长，本段已接受。';
    }

    updateUi();
  });

  finishBtn.addEventListener('click', async () => {
    const name = nameInput.value.trim();
    if (!name) {
      statusText = '请先填写姓名哦。';
      statusKind = 'error';
      updateUi();
      return;
    }

    if (utteranceCount < MIN_UTTERANCES) {
      statusText = `还需要至少 ${MIN_UTTERANCES} 段录音，加油！`;
      statusKind = 'error';
      updateUi();
      return;
    }

    finishBtn.disabled = true;
    const result = await bridge.completeEnrollment(name, utteranceCount);
    if (!result.ok) {
      statusText = result.error ?? '注册失败，请稍后重试';
      statusKind = 'error';
      finishBtn.disabled = false;
      updateUi();
      return;
    }

    statusText = '说话人注册成功，欢迎加入！';
    statusKind = 'success';
    updateUi();
    await bridge.requestClose(true);
  });

  updateUi();
}
