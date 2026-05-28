import {
  type AmrBridge,
  type AsrModelItem,
  type AudioDeviceItem,
  type ForcedIntent,
  type HudScreenCorner,
  type OnRefineFailure,
  type FeaturePresetDraft,
  type OptionalOverlaySkillItem,
  type SettingsDraft,
  type TriggerMode,
  type WakeWordSensitivity,
  getBridge,
  getBridgeSource,
  isMockBridge,
} from '../bridge';
import { escapeHtml, renderAppNav, wireAppNav } from '../layout/appShell';

type SectionId =
  | 'general'
  | 'audio'
  | 'speaker'
  | 'asr'
  | 'llm'
  | 'trigger'
  | 'feature'
  | 'paths';

interface ListData {
  devices: AudioDeviceItem[];
  speakers: Array<{ id: string; displayName: string; isNone: boolean }>;
  asrModels: AsrModelItem[];
  overlaySkills: OptionalOverlaySkillItem[];
  skillsMissing: string[];
}

const TRIGGER_MODE_OPTIONS: Array<{ value: TriggerMode; label: string }> = [
  { value: 'PttOnly', label: 'PTT（按住热键）' },
  { value: 'WakeWordOnly', label: '唤醒词' },
  { value: 'Both', label: 'PTT + 唤醒词' },
];

const WAKE_SENSITIVITY_OPTIONS: Array<{ value: WakeWordSensitivity; label: string }> = [
  { value: 'Standard', label: '标准' },
  { value: 'High', label: '高' },
  { value: 'Maximum', label: '最高（默认，小声/远距）' },
];

const HUD_CORNER_OPTIONS: Array<{ value: HudScreenCorner; label: string }> = [
  { value: 'BottomRight', label: '右下角' },
  { value: 'BottomLeft', label: '左下角' },
  { value: 'TopRight', label: '右上角' },
  { value: 'TopLeft', label: '左上角' },
];

const FORCED_INTENT_OPTIONS: Array<{ value: ForcedIntent; label: string }> = [
  { value: 'Auto', label: '自动判断（让 AI 选择）' },
  { value: 'PlainText', label: '纯文本整理 — 去口误、加标点' },
  { value: 'GeneralAi', label: '通用 AI Prompt' },
  { value: 'CodeEditing', label: '代码编辑指令' },
  { value: 'Research', label: '深度研究 Prompt' },
  { value: 'TaskPlan', label: '待办列表' },
];

const ON_REFINE_FAILURE_OPTIONS: Array<{ value: OnRefineFailure; label: string }> = [
  { value: 'UseRawTranscript', label: 'UseRawTranscript — 使用原始转写' },
  { value: 'ShowError', label: 'ShowError — 显示错误' },
  { value: 'KeepLast', label: 'KeepLast — 保留上次结果' },
];

const SECTION_NAV: Array<{ id: SectionId; label: string }> = [
  { id: 'general', label: '通用' },
  { id: 'audio', label: '录音设备' },
  { id: 'speaker', label: '声纹用户' },
  { id: 'asr', label: 'ASR 模型' },
  { id: 'llm', label: 'LLM 预设' },
  { id: 'trigger', label: '触发与 HUD' },
  { id: 'feature', label: '功能预设' },
  { id: 'paths', label: '目录' },
];

function optionTags<T extends string>(
  options: Array<{ value: T; label: string }>,
  selected: T,
): string {
  return options
    .map(
      (opt) =>
        `<option value="${escapeHtml(opt.value)}"${opt.value === selected ? ' selected' : ''}>${escapeHtml(opt.label)}</option>`,
    )
    .join('');
}

function wakeModeActive(mode: TriggerMode): boolean {
  return mode === 'WakeWordOnly' || mode === 'Both';
}

function fieldErrorClass(errors: Map<string, string>, field: string): string {
  return errors.has(field) ? ' is-error' : '';
}

function fieldErrorHtml(errors: Map<string, string>, field: string): string {
  const msg = errors.get(field);
  return msg ? `<p class="field-error">${escapeHtml(msg)}</p>` : '';
}

function scrollSettingsSection(root: HTMLElement, sectionId: SectionId): void {
  const container = root.querySelector<HTMLElement>('.settings-sections');
  const section = root.querySelector<HTMLElement>(`#section-${sectionId}`);
  if (!container || !section) return;

  const top =
    section.getBoundingClientRect().top -
    container.getBoundingClientRect().top +
    container.scrollTop;
  container.scrollTo({ top: Math.max(0, top - 16), behavior: 'smooth' });
}

function setTestConnectionUi(root: HTMLElement, running: boolean): void {
  const testBtn = root.querySelector<HTMLButtonElement>('#btnTestLlm');
  const saveBtn = root.querySelector<HTMLButtonElement>('#btnSave');
  if (testBtn) {
    testBtn.disabled = running;
    testBtn.textContent = running ? '测试中…' : '测试连接';
  }
  if (saveBtn) saveBtn.disabled = running;
}

export async function mountSettingsPage(root: HTMLElement): Promise<void> {
  root.innerHTML = `<div class="app-shell"><main class="app-content"><p>加载设置…</p></main></div>`;

  const bridge = await getBridge();
  let draft = await bridge.loadSettingsDraft();
  if (!draft.featurePresets?.length) {
    draft = {
      ...draft,
      featurePresets: [
        {
          name: '默认',
          llmPresetName: draft.llmPresets[0]?.name ?? '预设1',
          forcedIntent: draft.forcedIntent,
          onRefineFailure: draft.onRefineFailure,
          optionalOverlaySkills: [...draft.optionalOverlaySkills],
        },
      ],
      selectedFeaturePresetIndex: 0,
    };
  }
  const runtime = await bridge.getRuntimeState().catch(() => null);
  if (runtime?.triggerMode) {
    draft = { ...draft, triggerMode: runtime.triggerMode };
  }

  const appInfo = await bridge.getAppInfo().catch(() => ({ version: '—', platform: '—' }));

  const lists: ListData = {
    devices: await bridge.listAudioDevices().catch(() => []),
    speakers: await bridge.listSpeakerUsers().catch(() => []),
    asrModels: await bridge.listAsrModels().catch(() => []),
    overlaySkills: [],
    skillsMissing: [],
  };

  const initialFpOverlays =
    draft.featurePresets[draft.selectedFeaturePresetIndex]?.optionalOverlaySkills ??
    draft.optionalOverlaySkills;
  await refreshSkillsLists(bridge, draft.skillsDirectory, lists, initialFpOverlays);

  let activeSection: SectionId = 'general';
  let testConnectionRunning = false;
  const fieldErrors = new Map<string, string>();

  const render = (): void => {
    const presetIdx = Math.min(
      Math.max(draft.selectedLlmPresetIndex, 0),
      Math.max(draft.llmPresets.length - 1, 0),
    );
    const preset = draft.llmPresets[presetIdx];
    const fpIdx = Math.min(
      Math.max(draft.selectedFeaturePresetIndex, 0),
      Math.max(draft.featurePresets.length - 1, 0),
    );
    const fp = draft.featurePresets[fpIdx];
    const llmPresetNameOptions = draft.llmPresets.map((p) => p.name);
    const wakeVisible = wakeModeActive(draft.triggerMode);
    const globalError = fieldErrors.get('_global');
    const mockBanner = isMockBridge()
      ? `<div class="status-banner status-banner--warn">开发模式：mock bridge（${escapeHtml(getBridgeSource())}）。集成 WebUiBridge 后使用 hostObjects.amr。</div>`
      : '';
    const globalErrorBanner = globalError
      ? `<div class="status-banner status-banner--error">${escapeHtml(globalError)}</div>`
      : '';

    const selectedAsr = lists.asrModels.find((m) => m.id === draft.selectedAsrModelId);
    const asrStatus = selectedAsr
      ? selectedAsr.installed
        ? '✓ 已安装'
        : '✗ 未安装 — 保存前请在宿主环境下载模型'
      : '';

    root.innerHTML = `
      <div class="app-shell app-shell--settings">
        ${renderAppNav('settings')}
        <main class="app-content settings-page">
          <div class="settings-inner">
            <nav class="settings-section-nav" aria-label="设置分区">
              <p class="settings-section-nav__meta">${escapeHtml(appInfo.version)}</p>
              <ul class="settings-section-nav__list">
                ${SECTION_NAV.map(
                  (item) => `
                  <li>
                    <button type="button" class="settings-section-nav__link${activeSection === item.id ? ' is-active' : ''}" data-section-nav="${item.id}">
                      ${escapeHtml(item.label)}
                    </button>
                  </li>`,
                ).join('')}
              </ul>
            </nav>

            <div class="settings-sections">
              ${mockBanner}
              ${globalErrorBanner}

              <section id="section-general" class="settings-section card">
                <h2 class="card-title">通用</h2>
                <p class="card-subtitle">总开关与输出行为（托盘菜单对应项）。</p>
                <div class="form-grid">
                  <div class="form-check">
                    <input type="checkbox" id="masterEnabled"${draft.masterEnabled ? ' checked' : ''} />
                    <label for="masterEnabled">启用语音输入</label>
                  </div>
                  <div class="form-check">
                    <input type="checkbox" id="pasteToCaretEnabled"${draft.pasteToCaretEnabled ? ' checked' : ''}${!draft.masterEnabled ? ' disabled' : ''} />
                    <label for="pasteToCaretEnabled">粘贴到光标（否则仅复制到剪贴板）</label>
                  </div>
                  <div class="form-check">
                    <input type="checkbox" id="launchAtStartup"${draft.launchAtStartup ? ' checked' : ''} />
                    <label for="launchAtStartup">开机自启</label>
                  </div>
                </div>
              </section>

              <section id="section-audio" class="settings-section card">
                <h2 class="card-title">录音设备</h2>
                <div class="form-field${fieldErrorClass(fieldErrors, 'selectedDeviceId')}">
                  <label for="selectedDeviceId">输入设备</label>
                  <select id="selectedDeviceId">
                    ${lists.devices.length === 0 ? '<option value="">(无可用设备)</option>' : ''}
                    ${lists.devices
                      .map((d) => {
                        const selected =
                          draft.selectedDeviceId === d.id || (!draft.selectedDeviceId && d.isDefault);
                        return `<option value="${escapeHtml(d.id)}"${selected ? ' selected' : ''}>${escapeHtml(d.displayName)}${d.isDefault ? ' ★' : ''}</option>`;
                      })
                      .join('')}
                  </select>
                  ${fieldErrorHtml(fieldErrors, 'selectedDeviceId')}
                </div>
              </section>

              <section id="section-speaker" class="settings-section card">
                <h2 class="card-title">声纹用户</h2>
                <div class="form-grid">
                  <div class="form-field${fieldErrorClass(fieldErrors, 'currentSpeakerUserId')}">
                    <label for="currentSpeakerUserId">当前用户</label>
                    <select id="currentSpeakerUserId">
                      ${lists.speakers
                        .map((u) => {
                          const val = u.isNone ? '' : u.id;
                          const selected =
                            (u.isNone && !draft.currentSpeakerUserId) ||
                            draft.currentSpeakerUserId === u.id;
                          return `<option value="${escapeHtml(val)}"${selected ? ' selected' : ''}>${escapeHtml(u.displayName)}</option>`;
                        })
                        .join('')}
                    </select>
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, 'speakerVerifyThreshold')}">
                    <label for="speakerVerifyThreshold">声纹阈值（0.25 – 0.85）</label>
                    <input type="number" id="speakerVerifyThreshold" min="0.25" max="0.85" step="0.05" value="${draft.speakerVerifyThreshold}" />
                    ${fieldErrorHtml(fieldErrors, 'speakerVerifyThreshold')}
                  </div>
                </div>
              </section>

              <section id="section-asr" class="settings-section card">
                <h2 class="card-title">ASR 模型</h2>
                <div class="form-grid">
                  <div class="form-field${fieldErrorClass(fieldErrors, 'selectedAsrModelId')}">
                    <label for="selectedAsrModelId">SenseVoice 模型</label>
                    <select id="selectedAsrModelId">
                      ${lists.asrModels
                        .map(
                          (m) =>
                            `<option value="${escapeHtml(m.id)}"${draft.selectedAsrModelId === m.id ? ' selected' : ''}>${escapeHtml(m.displayName)}${m.installed ? ' ✓' : ' (未安装)'}</option>`,
                        )
                        .join('')}
                    </select>
                    <p class="form-hint">${escapeHtml(asrStatus)}</p>
                    ${fieldErrorHtml(fieldErrors, 'selectedAsrModelId')}
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, 'modelsDirectory')}">
                    <label for="modelsDirectory">模型目录</label>
                    <input type="text" id="modelsDirectory" value="${escapeHtml(draft.modelsDirectory)}" />
                    ${fieldErrorHtml(fieldErrors, 'modelsDirectory')}
                  </div>
                </div>
              </section>

              <section id="section-llm" class="settings-section card">
                <h2 class="card-title">LLM 预设</h2>
                <p class="card-subtitle">三组 API 预设；切换前自动保存当前编辑内容。</p>
                <div class="preset-tabs" role="tablist">
                  ${draft.llmPresets
                    .map(
                      (p, i) =>
                        `<button type="button" class="preset-tab${i === presetIdx ? ' is-active' : ''}" data-preset-index="${i}">预设 ${i + 1}: ${escapeHtml(p.name || `预设${i + 1}`)}</button>`,
                    )
                    .join('')}
                </div>
                <div class="form-grid">
                  <div class="form-field${fieldErrorClass(fieldErrors, 'presetName')}">
                    <label for="presetName">预设名称</label>
                    <input type="text" id="presetName" value="${escapeHtml(preset?.name ?? '')}" />
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, 'apiBaseUrl')}">
                    <label for="apiBaseUrl">API Base URL</label>
                    <input type="url" id="apiBaseUrl" value="${escapeHtml(preset?.apiBaseUrl ?? '')}" autocomplete="off" />
                    ${fieldErrorHtml(fieldErrors, 'apiBaseUrl')}
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, 'apiKey')}">
                    <label for="apiKey">API Key</label>
                    <input type="password" id="apiKey" value="${escapeHtml(preset?.apiKey ?? '')}" autocomplete="off" placeholder="本机 Ollama 等可留空" />
                    ${fieldErrorHtml(fieldErrors, 'apiKey')}
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, 'apiModel')}">
                    <label for="apiModel">Model</label>
                    <input type="text" id="apiModel" value="${escapeHtml(preset?.apiModel ?? '')}" autocomplete="off" />
                  </div>
                  <div class="form-row">
                    <button type="button" class="btn-ghost" id="btnTestLlm"${testConnectionRunning ? ' disabled' : ''}>
                      ${testConnectionRunning ? '测试中…' : '测试连接'}
                    </button>
                  </div>
                  <p class="test-result" id="testResult" aria-live="polite"></p>
                </div>
              </section>

              <section id="section-trigger" class="settings-section card">
                <h2 class="card-title">触发与 HUD</h2>
                <div class="form-grid">
                  <div class="form-field">
                    <label for="triggerMode">触发模式</label>
                    <select id="triggerMode">${optionTags(TRIGGER_MODE_OPTIONS, draft.triggerMode)}</select>
                  </div>

                  <div id="wakeSection" class="form-grid wake-section${wakeVisible ? '' : ' is-hidden'}">
                    <h3 class="wake-section__title">唤醒词</h3>
                    <div class="form-field${fieldErrorClass(fieldErrors, 'wakeWordPhrase')}">
                      <label for="wakeWordPhrase">唤醒词文本</label>
                      <input type="text" id="wakeWordPhrase" value="${escapeHtml(draft.wakeWordPhrase)}"${wakeVisible ? '' : ' disabled'} />
                      ${fieldErrorHtml(fieldErrors, 'wakeWordPhrase')}
                    </div>
                    <div class="form-field">
                      <label for="wakeWordSensitivity">唤醒灵敏度</label>
                      <select id="wakeWordSensitivity"${wakeVisible ? '' : ' disabled'}>${optionTags(WAKE_SENSITIVITY_OPTIONS, draft.wakeWordSensitivity)}</select>
                    </div>
                    <div class="form-field">
                      <label for="wakeCommandSilenceMs">指令结束静音（ms）</label>
                      <input type="number" id="wakeCommandSilenceMs" min="800" max="8000" step="200" value="${draft.wakeCommandSilenceMs}"${wakeVisible ? '' : ' disabled'} />
                      <p class="form-hint">说完指令后，连续静音达到该时长即提交（不含 ASR 识别耗时）。</p>
                    </div>
                    <div class="form-check">
                      <input type="checkbox" id="wakeUseVadEndDetection"${draft.wakeUseVadEndDetection ? ' checked' : ''}${wakeVisible ? '' : ' disabled'} />
                      <label for="wakeUseVadEndDetection">使用 VAD 尾部分析结束唤醒听写</label>
                    </div>
                  </div>

                  <div class="form-field">
                    <label for="hudScreenCorner">HUD 位置</label>
                    <select id="hudScreenCorner">${optionTags(HUD_CORNER_OPTIONS, draft.hudScreenCorner)}</select>
                  </div>

                  <div class="form-field${fieldErrorClass(fieldErrors, 'pttHotkey')}">
                    <span class="form-label">PTT 热键</span>
                    <div class="hotkey-field">
                      <input type="text" id="pttHotkey" readonly value="${escapeHtml(draft.pttHotkey)}" />
                      <button type="button" class="btn-ghost btn-sm" id="btnCaptureHotkey">点击录入…</button>
                    </div>
                    <p class="form-hint">通过原生对话框录入；Web 不监听全局热键。</p>
                    ${fieldErrorHtml(fieldErrors, 'pttHotkey')}
                  </div>
                </div>
              </section>

              <section id="section-feature" class="settings-section card">
                <h2 class="card-title">功能预设</h2>
                <p class="card-subtitle">将 LLM 预设（API）与整理风格、叠加 skill 组合为一键切换的功能配置；保存后写入 settings.json。</p>
                <div class="form-grid">
                  <div class="form-field">
                    <label for="activeFeaturePreset">当前使用的功能预设</label>
                    <select id="activeFeaturePreset">
                      ${draft.featurePresets
                        .map(
                          (p, i) =>
                            `<option value="${i}"${i === fpIdx ? ' selected' : ''}>${escapeHtml(p.name)}</option>`,
                        )
                        .join('')}
                    </select>
                    <p class="form-hint">切换后编辑下方字段并保存；托盘「功能模式」可快速切换。</p>
                  </div>
                  <div class="feature-preset-toolbar">
                    <button type="button" class="btn-ghost btn-sm" id="btnAddFeaturePreset">新建预设</button>
                    <button type="button" class="btn-ghost btn-sm" id="btnDeleteFeaturePreset"${draft.featurePresets.length <= 1 ? ' disabled' : ''}>删除当前</button>
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, `featurePresets[${fpIdx}].name`)}">
                    <label for="featurePresetName">预设名称</label>
                    <input type="text" id="featurePresetName" value="${escapeHtml(fp.name)}" />
                    ${fieldErrorHtml(fieldErrors, `featurePresets[${fpIdx}].name`)}
                  </div>
                  <div class="form-field${fieldErrorClass(fieldErrors, `featurePresets[${fpIdx}].llmPresetName`)}">
                    <label for="featurePresetLlm">关联 LLM 预设</label>
                    <select id="featurePresetLlm">
                      ${llmPresetNameOptions
                        .map(
                          (name) =>
                            `<option value="${escapeHtml(name)}"${name === fp.llmPresetName ? ' selected' : ''}>${escapeHtml(name)}</option>`,
                        )
                        .join('')}
                    </select>
                    ${fieldErrorHtml(fieldErrors, `featurePresets[${fpIdx}].llmPresetName`)}
                  </div>
                  <div class="form-field">
                    <label for="featurePresetIntent">整理风格</label>
                    <select id="featurePresetIntent">${optionTags(FORCED_INTENT_OPTIONS, fp.forcedIntent)}</select>
                  </div>
                  <div class="form-field">
                    <label for="featurePresetOnFailure">整理失败时</label>
                    <select id="featurePresetOnFailure">${optionTags(ON_REFINE_FAILURE_OPTIONS, fp.onRefineFailure)}</select>
                  </div>
                  <div class="form-field">
                    <span class="form-label">附加叠加 skill</span>
                    <div class="checkbox-list" id="featurePresetOverlaySkills">
                      ${
                        lists.overlaySkills.length === 0
                          ? '<span class="form-hint">（无 optional skills 或目录无效）</span>'
                          : lists.overlaySkills
                              .map(
                                (s) => `
                        <label>
                          <input type="checkbox" data-fp-overlay-key="${escapeHtml(s.key)}"${fp.optionalOverlaySkills.includes(s.key) ? ' checked' : ''} />
                          ${escapeHtml(s.label || s.key)}
                        </label>`,
                              )
                              .join('')
                      }
                    </div>
                  </div>
                </div>
              </section>

              <section id="section-paths" class="settings-section card">
                <h2 class="card-title">目录</h2>
                <div class="form-grid">
                  <div class="form-field${fieldErrorClass(fieldErrors, 'skillsDirectory')}">
                    <label for="skillsDirectory">Skills 目录</label>
                    <input type="text" id="skillsDirectory" value="${escapeHtml(draft.skillsDirectory)}" />
                    ${fieldErrorHtml(fieldErrors, 'skillsDirectory')}
                  </div>
                  ${
                    lists.skillsMissing.length > 0
                      ? `<div class="status-banner status-banner--error">缺少 skill 文件: ${escapeHtml(lists.skillsMissing.join(', '))}</div>`
                      : ''
                  }
                </div>
              </section>
            </div>
          </div>

          <footer class="settings-footer">
            <span class="settings-footer__meta">Array Mic Refreshment · ${escapeHtml(appInfo.version)}</span>
            <div class="settings-footer__actions">
              <button type="button" class="btn-ghost" id="btnCancel">取消</button>
              <button type="button" class="btn-primary" id="btnSave"${testConnectionRunning ? ' disabled' : ''}>保存</button>
            </div>
          </footer>
        </main>
      </div>
    `;

    wireAppNav(root, 'settings');
    bindEvents();
  };

  function savePresetFieldsFromDom(): void {
    const idx = draft.selectedLlmPresetIndex;
    if (idx < 0 || idx >= draft.llmPresets.length) return;
    const preset = draft.llmPresets[idx];
    preset.name = root.querySelector<HTMLInputElement>('#presetName')?.value.trim() ?? preset.name;
    preset.apiBaseUrl = root.querySelector<HTMLInputElement>('#apiBaseUrl')?.value.trim() ?? preset.apiBaseUrl;
    preset.apiKey = root.querySelector<HTMLInputElement>('#apiKey')?.value ?? preset.apiKey;
    preset.apiModel = root.querySelector<HTMLInputElement>('#apiModel')?.value.trim() ?? preset.apiModel;
  }

  function saveFeaturePresetFieldsFromDom(): void {
    if (draft.featurePresets.length === 0) return;
    const idx = Math.min(
      Math.max(
        parseInt(root.querySelector<HTMLSelectElement>('#activeFeaturePreset')?.value ?? '0', 10),
        0,
      ),
      draft.featurePresets.length - 1,
    );
    draft.selectedFeaturePresetIndex = idx;
    const fp = draft.featurePresets[idx];
    fp.name = root.querySelector<HTMLInputElement>('#featurePresetName')?.value.trim() ?? fp.name;
    fp.llmPresetName =
      root.querySelector<HTMLSelectElement>('#featurePresetLlm')?.value ?? fp.llmPresetName;
    fp.forcedIntent =
      (root.querySelector<HTMLSelectElement>('#featurePresetIntent')?.value as ForcedIntent) ??
      fp.forcedIntent;
    fp.onRefineFailure =
      (root.querySelector<HTMLSelectElement>('#featurePresetOnFailure')?.value as OnRefineFailure) ??
      fp.onRefineFailure;
    const overlay: string[] = [];
    root.querySelectorAll<HTMLInputElement>('[data-fp-overlay-key]').forEach((el) => {
      if (el.checked && el.dataset.fpOverlayKey) overlay.push(el.dataset.fpOverlayKey);
    });
    fp.optionalOverlaySkills = overlay;
  }

  function readDraftFromDom(): SettingsDraft {
    savePresetFieldsFromDom();
    saveFeaturePresetFieldsFromDom();

    const deviceSelect = root.querySelector<HTMLSelectElement>('#selectedDeviceId');
    const speakerSelect = root.querySelector<HTMLSelectElement>('#currentSpeakerUserId');

    return {
      ...draft,
      masterEnabled: root.querySelector<HTMLInputElement>('#masterEnabled')?.checked ?? draft.masterEnabled,
      pasteToCaretEnabled:
        root.querySelector<HTMLInputElement>('#pasteToCaretEnabled')?.checked ?? draft.pasteToCaretEnabled,
      launchAtStartup: root.querySelector<HTMLInputElement>('#launchAtStartup')?.checked ?? draft.launchAtStartup,
      promptRefineEnabled: draft.featurePresets.length > 0 ? true : draft.promptRefineEnabled,
      forcedIntent: draft.featurePresets[draft.selectedFeaturePresetIndex]?.forcedIntent ?? draft.forcedIntent,
      onRefineFailure:
        draft.featurePresets[draft.selectedFeaturePresetIndex]?.onRefineFailure ?? draft.onRefineFailure,
      selectedDeviceId: deviceSelect?.value || null,
      currentSpeakerUserId: speakerSelect?.value || null,
      speakerVerifyThreshold: parseFloat(
        root.querySelector<HTMLInputElement>('#speakerVerifyThreshold')?.value ??
          String(draft.speakerVerifyThreshold),
      ),
      selectedAsrModelId:
        root.querySelector<HTMLSelectElement>('#selectedAsrModelId')?.value ?? draft.selectedAsrModelId,
      skillsDirectory:
        root.querySelector<HTMLInputElement>('#skillsDirectory')?.value.trim() ?? draft.skillsDirectory,
      modelsDirectory:
        root.querySelector<HTMLInputElement>('#modelsDirectory')?.value.trim() ?? draft.modelsDirectory,
      triggerMode:
        (root.querySelector<HTMLSelectElement>('#triggerMode')?.value as TriggerMode) ?? draft.triggerMode,
      wakeWordPhrase:
        root.querySelector<HTMLInputElement>('#wakeWordPhrase')?.value.trim() ?? draft.wakeWordPhrase,
      wakeWordSensitivity:
        (root.querySelector<HTMLSelectElement>('#wakeWordSensitivity')?.value as WakeWordSensitivity) ??
        draft.wakeWordSensitivity,
      wakeCommandSilenceMs: parseInt(
        root.querySelector<HTMLInputElement>('#wakeCommandSilenceMs')?.value ??
          String(draft.wakeCommandSilenceMs),
        10,
      ),
      wakeUseVadEndDetection:
        root.querySelector<HTMLInputElement>('#wakeUseVadEndDetection')?.checked ??
        draft.wakeUseVadEndDetection,
      hudScreenCorner:
        (root.querySelector<HTMLSelectElement>('#hudScreenCorner')?.value as HudScreenCorner) ??
        draft.hudScreenCorner,
      pttHotkey: root.querySelector<HTMLInputElement>('#pttHotkey')?.value ?? draft.pttHotkey,
      optionalOverlaySkills:
        draft.featurePresets[draft.selectedFeaturePresetIndex]?.optionalOverlaySkills ??
        draft.optionalOverlaySkills,
      llmPresets: draft.llmPresets,
      selectedLlmPresetIndex: draft.selectedLlmPresetIndex,
      featurePresets: draft.featurePresets,
      selectedFeaturePresetIndex: draft.selectedFeaturePresetIndex,
    };
  }

  function bindEvents(): void {
    root.querySelectorAll<HTMLButtonElement>('[data-section-nav]').forEach((btn) => {
      btn.addEventListener('click', () => {
        draft = readDraftFromDom();
        activeSection = btn.dataset.sectionNav as SectionId;
        root.querySelectorAll('.settings-section-nav__link').forEach((link) => {
          link.classList.toggle('is-active', link === btn);
        });
        scrollSettingsSection(root, activeSection);
      });
    });

    root.querySelector('#masterEnabled')?.addEventListener('change', (e) => {
      const paste = root.querySelector<HTMLInputElement>('#pasteToCaretEnabled');
      if (paste) paste.disabled = !(e.target as HTMLInputElement).checked;
    });

    root.querySelector('#triggerMode')?.addEventListener('change', () => {
      draft = readDraftFromDom();
      render();
    });

    root.querySelector('#skillsDirectory')?.addEventListener('change', () => {
      void (async () => {
        draft = readDraftFromDom();
        const fpOverlays =
          draft.featurePresets[draft.selectedFeaturePresetIndex]?.optionalOverlaySkills ??
          draft.optionalOverlaySkills;
        await refreshSkillsLists(bridge, draft.skillsDirectory, lists, fpOverlays);
        render();
      })();
    });

    root.querySelectorAll<HTMLButtonElement>('[data-preset-index]').forEach((btn) => {
      btn.addEventListener('click', () => {
        savePresetFieldsFromDom();
        draft.selectedLlmPresetIndex = parseInt(btn.dataset.presetIndex ?? '0', 10);
        render();
      });
    });

    root.querySelector('#activeFeaturePreset')?.addEventListener('change', () => {
      saveFeaturePresetFieldsFromDom();
      draft.selectedFeaturePresetIndex = parseInt(
        root.querySelector<HTMLSelectElement>('#activeFeaturePreset')?.value ?? '0',
        10,
      );
      render();
    });

    root.querySelector('#btnAddFeaturePreset')?.addEventListener('click', () => {
      saveFeaturePresetFieldsFromDom();
      const next: FeaturePresetDraft = {
        name: `功能预设 ${draft.featurePresets.length + 1}`,
        llmPresetName: draft.llmPresets[0]?.name ?? '预设1',
        forcedIntent: 'PlainText',
        onRefineFailure: 'UseRawTranscript',
        optionalOverlaySkills: [],
      };
      draft.featurePresets.push(next);
      draft.selectedFeaturePresetIndex = draft.featurePresets.length - 1;
      render();
    });

    root.querySelector('#btnDeleteFeaturePreset')?.addEventListener('click', () => {
      if (draft.featurePresets.length <= 1) return;
      saveFeaturePresetFieldsFromDom();
      const removeIdx = draft.selectedFeaturePresetIndex;
      draft.featurePresets.splice(removeIdx, 1);
      draft.selectedFeaturePresetIndex = Math.min(removeIdx, draft.featurePresets.length - 1);
      render();
    });

    root.querySelector('#btnCaptureHotkey')?.addEventListener('click', () => {
      void (async () => {
        draft = readDraftFromDom();
        const result = await bridge.openHotkeyCaptureDialog(draft.pttHotkey);
        if (!result.cancelled) {
          draft.pttHotkey = result.hotkey;
          const input = root.querySelector<HTMLInputElement>('#pttHotkey');
          if (input) input.value = result.hotkey;
        }
      })();
    });

    root.querySelector('#btnTestLlm')?.addEventListener('click', () => void handleTestConnection());
    root.querySelector('#btnSave')?.addEventListener('click', () => void handleSave());
    root.querySelector('#btnCancel')?.addEventListener('click', () => void handleCancel());
  }

  async function handleTestConnection(): Promise<void> {
    if (testConnectionRunning) return;
    testConnectionRunning = true;
    setTestConnectionUi(root, true);

    try {
      const payload = readDraftFromDom();
      payload.promptRefineEnabled = true;
      const result = await bridge.testLlmConnection(payload);
      const el = root.querySelector('#testResult');
      if (el) {
        el.textContent = result.message;
        el.className = `test-result${result.ok ? ' is-success' : ' is-error'}`;
      }
    } catch (err) {
      const el = root.querySelector('#testResult');
      const msg = err instanceof Error ? err.message : String(err);
      if (el) {
        el.textContent = `失败: ${msg}`;
        el.className = 'test-result is-error';
      }
    } finally {
      testConnectionRunning = false;
      setTestConnectionUi(root, false);
    }
  }

  async function handleSave(): Promise<void> {
    if (testConnectionRunning) return;
    draft = readDraftFromDom();
    fieldErrors.clear();

    try {
      const validation = await bridge.validateSettingsDraft(draft);
      if (!validation.ok) {
        for (const err of validation.errors) fieldErrors.set(err.field, err.message);
        render();
        return;
      }

      const save = await bridge.saveSettingsDraft(draft);
      if (!save.ok) {
        fieldErrors.set('_global', save.error ?? '保存失败');
        render();
        return;
      }

      window.chrome?.webview?.postMessage?.(JSON.stringify({ type: 'settingsSaved', ok: true }));
    } catch (err) {
      fieldErrors.set('_global', err instanceof Error ? err.message : String(err));
      render();
    }
  }

  async function handleCancel(): Promise<void> {
    draft = await bridge.loadSettingsDraft();
    if (runtime?.triggerMode) {
      draft = { ...draft, triggerMode: runtime.triggerMode };
    }
    fieldErrors.clear();
    const cancelFpOverlays =
      draft.featurePresets[draft.selectedFeaturePresetIndex]?.optionalOverlaySkills ??
      draft.optionalOverlaySkills;
    await refreshSkillsLists(bridge, draft.skillsDirectory, lists, cancelFpOverlays);
    render();
  }

  render();
}

async function refreshSkillsLists(
  bridge: AmrBridge,
  skillsDirectory: string,
  lists: ListData,
  selected: string[],
): Promise<void> {
  try {
    lists.overlaySkills = await bridge.listOptionalOverlaySkills(skillsDirectory);
    for (const item of lists.overlaySkills) {
      item.checked = selected.includes(item.key);
    }
    const status = await bridge.getSkillsCatalogStatus(skillsDirectory);
    lists.skillsMissing = status.missingFiles ?? [];
  } catch {
    lists.overlaySkills = [];
    lists.skillsMissing = [];
  }
}
