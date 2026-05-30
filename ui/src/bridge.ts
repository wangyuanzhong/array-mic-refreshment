/** Settings draft DTO — mirrors docs/UI_ROUTE_B_WEBVIEW2.md §7.3 and AppSettings. */
export type ForcedIntent =
  | 'Auto'
  | 'PlainText'
  | 'GeneralAi'
  | 'CodeEditing'
  | 'Research'
  | 'TaskPlan';

export type OnRefineFailure = 'UseRawTranscript' | 'ShowError' | 'KeepLast';

export type TriggerMode = 'PttOnly' | 'Manual' | 'WakeWordOnly' | 'Both';

export type WakeWordSensitivity = 'Standard' | 'High' | 'Maximum';

export type HudScreenCorner = 'BottomRight' | 'BottomLeft' | 'TopRight' | 'TopLeft';

export interface LlmPresetDraft {
  name: string;
  apiBaseUrl: string;
  apiKey: string;
  apiModel: string;
}

export interface FeaturePresetDraft {
  name: string;
  llmPresetName: string;
  forcedIntent: ForcedIntent;
  /** Specialist key: auto, plain-text, code-editing, or user style id. */
  forcedSpecialistKey: string;
  onRefineFailure: OnRefineFailure;
  optionalOverlaySkills: string[];
}

export interface SettingsDraft {
  masterEnabled: boolean;
  pasteToCaretEnabled: boolean;
  launchAtStartup: boolean;
  promptRefineEnabled: boolean;
  forcedIntent: ForcedIntent;
  forcedSpecialistKey: string;
  onRefineFailure: OnRefineFailure;

  selectedDeviceId: string | null;
  currentSpeakerUserId: string | null;
  speakerVerifyThreshold: number;

  selectedAsrModelId: string;
  skillsDirectory: string;
  modelsDirectory: string;

  triggerMode: TriggerMode;
  wakeWordPhrase: string;
  wakeWordSensitivity: WakeWordSensitivity;
  wakeCommandSilenceMs: number;
  wakeUseVadEndDetection: boolean;

  hudScreenCorner: HudScreenCorner;
  useWebStatusHud: boolean;
  pttHotkey: string;

  selectedLlmPresetIndex: number;
  llmPresets: LlmPresetDraft[];
  optionalOverlaySkills: string[];
  selectedFeaturePresetIndex: number;
  featurePresets: FeaturePresetDraft[];
}

export interface AppInfo {
  version: string;
  platform: string;
}

export interface RuntimeState {
  triggerMode: TriggerMode;
  masterEnabled: boolean;
}

export interface AudioDeviceItem {
  id: string;
  displayName: string;
  isDefault: boolean;
}

export interface SpeakerUserItem {
  id: string;
  displayName: string;
  isNone: boolean;
}

export interface AsrModelItem {
  id: string;
  displayName: string;
  installed: boolean;
}

export interface WakeWordModelStatus {
  displayName: string;
  installed: boolean;
  engineReady: boolean;
  resolvedPath: string;
  builtinPhrases?: string[];
}

export interface OptionalOverlaySkillItem {
  key: string;
  label: string;
  checked: boolean;
}

export interface SkillsCatalogStatus {
  missingFiles: string[];
}

export interface RefinementStyleItem {
  key: string;
  name: string;
  description: string;
  deletable: boolean;
  fileName?: string | null;
}

export interface AddRefinementStyleResult {
  ok: boolean;
  cancelled?: boolean;
  key?: string;
  styles?: RefinementStyleItem[];
  error?: string;
}

export interface DeleteRefinementStyleResult {
  ok: boolean;
  styles?: RefinementStyleItem[];
  error?: string;
}

export interface ValidationError {
  field: string;
  message: string;
}

export interface ValidateResult {
  ok: boolean;
  errors: ValidationError[];
}

export interface SaveResult {
  ok: boolean;
  error?: string;
  warning?: string;
}

export interface ApplyPttHotkeyResult {
  ok: boolean;
  activeHotkey: string;
  error?: string;
}

export interface TestLlmResult {
  ok: boolean;
  message: string;
  elapsedMs?: number;
}

export interface HotkeyCaptureResult {
  hotkey: string;
  cancelled: boolean;
}

export interface FolderPickerResult {
  path: string;
  cancelled: boolean;
}

export interface StartEnrollmentResult {
  ok: boolean;
  error?: string;
}

export interface StopEnrollmentResult {
  durationMs: number;
  ok: boolean;
  message?: string;
  utteranceCount: number;
}

export interface CompleteEnrollmentResult {
  ok: boolean;
  userId?: string;
  error?: string;
}

export interface PrivacyConsentState {
  needsPrompt: boolean;
  host: string;
  apiBaseUrl: string;
  isLoopback: boolean;
  message: string;
}

export interface AcceptPrivacyResult {
  ok: boolean;
}

export interface FeaturePresetListItem {
  index: number;
  name: string;
  llmPresetName: string;
  forcedIntent: ForcedIntent;
  forcedSpecialistKey: string;
  onRefineFailure: OnRefineFailure;
  optionalOverlaySkills: string[];
  selected: boolean;
}

export interface ApplyFeaturePresetResult {
  ok: boolean;
  error?: string;
  warning?: string;
  selectedFeaturePresetIndex?: number;
}

/** Raw COM host object shape (WebView2 async proxy). */
export interface AmrHostObject {
  GetAppInfo(): Promise<string>;
  GetRuntimeState(): Promise<string>;
  ListAudioDevices(): Promise<string>;
  ListSpeakerUsers(): Promise<string>;
  ListAsrModels(): Promise<string>;
  GetWakeWordModelStatus(): Promise<string>;
  ListOptionalOverlaySkills(skillsDirectory: string): Promise<string>;
  GetSkillsCatalogStatus(skillsDirectory: string): Promise<string>;
  ListRefinementStyles(skillsDirectory: string): Promise<string>;
  AddRefinementStyle(skillsDirectory: string): Promise<string>;
  DeleteRefinementStyle(skillsDirectory: string, key: string): Promise<string>;
  LoadSettingsDraft(): Promise<string>;
  ValidateSettingsDraft(draftJson: string): Promise<string>;
  SaveSettingsDraft(draftJson: string): Promise<string>;
  TestLlmConnection(draftJson?: string): Promise<string>;
  OpenHotkeyCaptureDialog(currentHotkey: string): Promise<string>;
  ApplyPttHotkey(hotkey: string): Promise<string>;
  OpenFolderPickerDialog(initialPath: string): Promise<string>;
  StartEnrollmentUtterance(): Promise<string>;
  StopEnrollmentUtterance(): Promise<string>;
  CompleteEnrollment(name: string, utteranceCount: number): Promise<string>;
  GetPrivacyConsentState(apiBaseUrl: string): Promise<string>;
  AcceptPrivacy(host: string): Promise<string>;
  ListFeaturePresets(): Promise<string>;
  ApplyFeaturePreset(index: number): Promise<string>;
  RequestClose(success: boolean): Promise<void>;
}

export interface AmrBridge {
  getAppInfo(): Promise<AppInfo>;
  getRuntimeState(): Promise<RuntimeState>;
  listAudioDevices(): Promise<AudioDeviceItem[]>;
  listSpeakerUsers(): Promise<SpeakerUserItem[]>;
  listAsrModels(): Promise<AsrModelItem[]>;
  getWakeWordModelStatus(): Promise<WakeWordModelStatus>;
  listOptionalOverlaySkills(skillsDirectory: string): Promise<OptionalOverlaySkillItem[]>;
  getSkillsCatalogStatus(skillsDirectory: string): Promise<SkillsCatalogStatus>;
  listRefinementStyles(skillsDirectory: string): Promise<RefinementStyleItem[]>;
  addRefinementStyle(skillsDirectory: string): Promise<AddRefinementStyleResult>;
  deleteRefinementStyle(skillsDirectory: string, key: string): Promise<DeleteRefinementStyleResult>;
  loadSettingsDraft(): Promise<SettingsDraft>;
  validateSettingsDraft(draft: SettingsDraft): Promise<ValidateResult>;
  saveSettingsDraft(draft: SettingsDraft): Promise<SaveResult>;
  testLlmConnection(draft?: SettingsDraft): Promise<TestLlmResult>;
  openHotkeyCaptureDialog(currentHotkey: string): Promise<HotkeyCaptureResult>;
  applyPttHotkey(hotkey: string): Promise<ApplyPttHotkeyResult>;
  openFolderPickerDialog(initialPath: string): Promise<FolderPickerResult>;
  startEnrollmentUtterance(): Promise<StartEnrollmentResult>;
  stopEnrollmentUtterance(): Promise<StopEnrollmentResult>;
  completeEnrollment(name: string, utteranceCount: number): Promise<CompleteEnrollmentResult>;
  getPrivacyConsentState(apiBaseUrl: string): Promise<PrivacyConsentState>;
  acceptPrivacy(host: string): Promise<AcceptPrivacyResult>;
  listFeaturePresets(): Promise<FeaturePresetListItem[]>;
  applyFeaturePreset(index: number): Promise<ApplyFeaturePresetResult>;
  requestClose(success: boolean): Promise<void>;
}

export function createDefaultSettingsDraft(): SettingsDraft {
  return {
    masterEnabled: true,
    pasteToCaretEnabled: true,
    launchAtStartup: true,
    promptRefineEnabled: false,
    forcedIntent: 'PlainText',
    forcedSpecialistKey: 'plain-text',
    onRefineFailure: 'UseRawTranscript',
    selectedDeviceId: null,
    currentSpeakerUserId: null,
    speakerVerifyThreshold: 0.4,
    selectedAsrModelId: '',
    skillsDirectory: 'skills',
    modelsDirectory: 'models',
    triggerMode: 'PttOnly',
    wakeWordPhrase: '小助手',
    wakeWordSensitivity: 'Maximum',
    wakeCommandSilenceMs: 700,
    wakeUseVadEndDetection: true,
    hudScreenCorner: 'BottomRight',
    useWebStatusHud: false,
    pttHotkey: 'Ctrl+Alt+Space',
    selectedLlmPresetIndex: 0,
    llmPresets: [
      {
        name: '预设1',
        apiBaseUrl: 'https://api.openai.com/v1',
        apiKey: '',
        apiModel: 'gpt-4o-mini',
      },
      {
        name: '预设2',
        apiBaseUrl: 'https://api.openai.com/v1',
        apiKey: '',
        apiModel: 'gpt-4o-mini',
      },
      {
        name: '预设3',
        apiBaseUrl: 'https://api.openai.com/v1',
        apiKey: '',
        apiModel: 'gpt-4o-mini',
      },
    ],
    optionalOverlaySkills: [],
    selectedFeaturePresetIndex: 0,
    featurePresets: [
      {
        name: '默认',
        llmPresetName: '预设1',
        forcedIntent: 'PlainText',
        forcedSpecialistKey: 'plain-text',
        onRefineFailure: 'UseRawTranscript',
        optionalOverlaySkills: [],
      },
    ],
  };
}

/** Map legacy enum to manifest specialist key. */
export function forcedIntentToSpecialistKey(intent: ForcedIntent): string {
  switch (intent) {
    case 'Auto':
      return 'auto';
    case 'PlainText':
      return 'plain-text';
    case 'GeneralAi':
      return 'general-ai';
    case 'CodeEditing':
      return 'code-editing';
    case 'Research':
      return 'research';
    case 'TaskPlan':
      return 'task-plan';
    default:
      return 'plain-text';
  }
}

export function resolveFeaturePresetStyleKey(fp: FeaturePresetDraft): string {
  return fp.forcedSpecialistKey?.trim() || forcedIntentToSpecialistKey(fp.forcedIntent);
}

/** Shipped manifest specialists — keep in sync with skills/manifest.yaml (UI fallback). */
export const BUILTIN_REFINEMENT_STYLES: RefinementStyleItem[] = [
  {
    key: 'plain-text',
    name: '纯文本整理',
    description: '去除口误、加标点、修正语序，输出适合人类阅读的书面语（短 prompt，省 token）',
    deletable: false,
  },
  {
    key: 'general-ai',
    name: '通用 AI Prompt',
    description: '把语音整理成通用 AI 提示词（不扩写）',
    deletable: false,
  },
  {
    key: 'code-editing',
    name: '软件开发需求（产品视角）',
    description: '口述需求 → 页面/流程/步骤化说明；不写接口、框架、函数名（避免误导）',
    deletable: false,
  },
  {
    key: 'research',
    name: '深度研究 Prompt',
    description: '同语言、多角度拆解的长研究提示词（可适度拓宽主题）；非待办/非产品需求',
    deletable: false,
  },
  {
    key: 'task-plan',
    name: '待办列表',
    description: '口述 → 简短可执行待办（动词开头）；不是产品需求长文，也不是深度研究',
    deletable: false,
  },
];

function parseJson<T>(raw: unknown, label: string): T {
  try {
    if (typeof raw === 'string') {
      return JSON.parse(raw) as T;
    }

    if (raw !== null && typeof raw === 'object') {
      return raw as T;
    }

    throw new Error(`Unexpected ${label} payload type: ${typeof raw}`);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    throw new Error(`Failed to parse ${label}: ${message}`);
  }
}

function wrapHostObject(host: AmrHostObject): AmrBridge {
  return {
    async getAppInfo() {
      return parseJson<AppInfo>(await host.GetAppInfo(), 'GetAppInfo');
    },
    async getRuntimeState() {
      return parseJson<RuntimeState>(await host.GetRuntimeState(), 'GetRuntimeState');
    },
    async listAudioDevices() {
      return parseJson<AudioDeviceItem[]>(await host.ListAudioDevices(), 'ListAudioDevices');
    },
    async listSpeakerUsers() {
      return parseJson<SpeakerUserItem[]>(await host.ListSpeakerUsers(), 'ListSpeakerUsers');
    },
    async listAsrModels() {
      return parseJson<AsrModelItem[]>(await host.ListAsrModels(), 'ListAsrModels');
    },
    async getWakeWordModelStatus() {
      return parseJson<WakeWordModelStatus>(
        await host.GetWakeWordModelStatus(),
        'GetWakeWordModelStatus',
      );
    },
    async listOptionalOverlaySkills(skillsDirectory: string) {
      return parseJson<OptionalOverlaySkillItem[]>(
        await host.ListOptionalOverlaySkills(skillsDirectory),
        'ListOptionalOverlaySkills',
      );
    },
    async getSkillsCatalogStatus(skillsDirectory: string) {
      return parseJson<SkillsCatalogStatus>(
        await host.GetSkillsCatalogStatus(skillsDirectory),
        'GetSkillsCatalogStatus',
      );
    },
    async listRefinementStyles(skillsDirectory: string) {
      const listFn = (host as AmrHostObject & { ListRefinementStyles?: (dir: string) => Promise<string> })
        .ListRefinementStyles;
      if (typeof listFn !== 'function') {
        console.warn('[bridge] host.ListRefinementStyles missing — using built-in style list');
        return structuredClone(BUILTIN_REFINEMENT_STYLES);
      }

      try {
        const styles = parseJson<RefinementStyleItem[]>(
          await listFn.call(host, skillsDirectory),
          'ListRefinementStyles',
        );
        return styles.length > 0 ? styles : structuredClone(BUILTIN_REFINEMENT_STYLES);
      } catch (err) {
        console.warn('[bridge] ListRefinementStyles failed — using built-in style list', err);
        return structuredClone(BUILTIN_REFINEMENT_STYLES);
      }
    },
    async addRefinementStyle(skillsDirectory: string) {
      return parseJson<AddRefinementStyleResult>(
        await host.AddRefinementStyle(skillsDirectory),
        'AddRefinementStyle',
      );
    },
    async deleteRefinementStyle(skillsDirectory: string, key: string) {
      return parseJson<DeleteRefinementStyleResult>(
        await host.DeleteRefinementStyle(skillsDirectory, key),
        'DeleteRefinementStyle',
      );
    },
    async loadSettingsDraft() {
      return parseJson<SettingsDraft>(await host.LoadSettingsDraft(), 'LoadSettingsDraft');
    },
    async validateSettingsDraft(draft: SettingsDraft) {
      return parseJson<ValidateResult>(
        await host.ValidateSettingsDraft(JSON.stringify(draft)),
        'ValidateSettingsDraft',
      );
    },
    async saveSettingsDraft(draft: SettingsDraft) {
      return parseJson<SaveResult>(
        await host.SaveSettingsDraft(JSON.stringify(draft)),
        'SaveSettingsDraft',
      );
    },
    async testLlmConnection(draft?: SettingsDraft) {
      const json = draft ? JSON.stringify(draft) : undefined;
      return parseJson<TestLlmResult>(
        await host.TestLlmConnection(json ?? ''),
        'TestLlmConnection',
      );
    },
    async openHotkeyCaptureDialog(currentHotkey: string) {
      return parseJson<HotkeyCaptureResult>(
        await host.OpenHotkeyCaptureDialog(currentHotkey),
        'OpenHotkeyCaptureDialog',
      );
    },
    async applyPttHotkey(hotkey: string) {
      return parseJson<ApplyPttHotkeyResult>(
        await host.ApplyPttHotkey(hotkey),
        'ApplyPttHotkey',
      );
    },
    async openFolderPickerDialog(initialPath: string) {
      return parseJson<FolderPickerResult>(
        await host.OpenFolderPickerDialog(initialPath),
        'OpenFolderPickerDialog',
      );
    },
    async startEnrollmentUtterance() {
      return parseJson<StartEnrollmentResult>(
        await host.StartEnrollmentUtterance(),
        'StartEnrollmentUtterance',
      );
    },
    async stopEnrollmentUtterance() {
      return parseJson<StopEnrollmentResult>(
        await host.StopEnrollmentUtterance(),
        'StopEnrollmentUtterance',
      );
    },
    async completeEnrollment(name: string, utteranceCount: number) {
      return parseJson<CompleteEnrollmentResult>(
        await host.CompleteEnrollment(name, utteranceCount),
        'CompleteEnrollment',
      );
    },
    async getPrivacyConsentState(apiBaseUrl: string) {
      return parseJson<PrivacyConsentState>(
        await host.GetPrivacyConsentState(apiBaseUrl),
        'GetPrivacyConsentState',
      );
    },
    async acceptPrivacy(hostName: string) {
      return parseJson<AcceptPrivacyResult>(
        await host.AcceptPrivacy(hostName),
        'AcceptPrivacy',
      );
    },
    async listFeaturePresets() {
      return parseJson<FeaturePresetListItem[]>(
        await host.ListFeaturePresets(),
        'ListFeaturePresets',
      );
    },
    async applyFeaturePreset(index: number) {
      return parseJson<ApplyFeaturePresetResult>(
        await host.ApplyFeaturePreset(index),
        'ApplyFeaturePreset',
      );
    },
    async requestClose(success: boolean) {
      await host.RequestClose(success);
    },
  };
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Mock bridge for local Vite dev when WebView2 host is unavailable. */
function createMockBridge(): AmrBridge {
  let draft = createDefaultSettingsDraft();
  let mockUtteranceCount = 0;

  return {
    async getAppInfo() {
      await delay(80);
      return { version: 'V0.3-mock', platform: 'web-dev' };
    },
    async getRuntimeState() {
      await delay(40);
      return { triggerMode: draft.triggerMode, masterEnabled: draft.masterEnabled };
    },
    async listAudioDevices() {
      await delay(60);
      return [
        { id: 'default', displayName: '默认麦克风 (Mock)', isDefault: true },
        { id: 'device-2', displayName: 'USB 阵列麦 (Mock)', isDefault: false },
      ];
    },
    async listSpeakerUsers() {
      await delay(60);
      return [
        { id: '', displayName: '无用户（不做声纹识别）', isNone: true },
        { id: 'user-alice', displayName: 'Alice (Mock)', isNone: false },
      ];
    },
    async listAsrModels() {
      await delay(60);
      return [
        { id: 'sensevoice-small', displayName: 'SenseVoice Small', installed: true },
        { id: 'sensevoice-large', displayName: 'SenseVoice Large', installed: false },
      ];
    },
    async getWakeWordModelStatus() {
      await delay(40);
      return {
        displayName: 'sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01',
        installed: true,
        engineReady: true,
        resolvedPath: 'models\\sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01',
      };
    },
    async listOptionalOverlaySkills(skillsDirectory: string) {
      await delay(40);
      void skillsDirectory;
      return [
        { key: 'code_style', label: '代码风格 overlay (mock)', checked: false },
        { key: 'research_depth', label: '研究深度 overlay (mock)', checked: false },
      ];
    },
    async getSkillsCatalogStatus(skillsDirectory: string) {
      await delay(40);
      void skillsDirectory;
      return { missingFiles: [] };
    },
    async listRefinementStyles(_skillsDirectory: string) {
      await delay(40);
      return [
        { key: 'plain-text', name: '纯文本整理', description: 'Mock 去口误、加标点', deletable: false },
        { key: 'general-ai', name: '通用 AI Prompt', description: 'Mock 通用提示词', deletable: false },
        { key: 'code-editing', name: '软件开发需求（产品视角）', description: 'Mock 产品需求', deletable: false },
      ];
    },
    async addRefinementStyle(skillsDirectory: string) {
      await delay(80);
      const path = window.prompt('Mock 添加整理风格：输入 .md 绝对路径', 'C:\\temp\\style.md');
      if (!path) return { ok: false, cancelled: true };
      void skillsDirectory;
      const styles = await this.listRefinementStyles(skillsDirectory);
      return {
        ok: true,
        key: 'mock-custom',
        styles: [
          ...styles,
          { key: 'mock-custom', name: 'Mock 自定义', description: path, deletable: true, fileName: 'mock.md' },
        ],
      };
    },
    async deleteRefinementStyle(skillsDirectory: string, key: string) {
      await delay(80);
      void skillsDirectory;
      const styles = (await this.listRefinementStyles(skillsDirectory)).filter((s) => s.key !== key);
      return { ok: true, styles };
    },
    async loadSettingsDraft() {
      await delay(100);
      return structuredClone(draft);
    },
    async validateSettingsDraft(next) {
      await delay(120);
      const errors: ValidationError[] = [];
      if (
        (next.triggerMode === 'WakeWordOnly' || next.triggerMode === 'Both') &&
        !next.wakeWordPhrase.trim()
      ) {
        errors.push({ field: 'wakeWordPhrase', message: '唤醒词模式下请填写唤醒词文本。' });
      }
      if (next.promptRefineEnabled && !next.llmPresets[next.selectedLlmPresetIndex]?.apiBaseUrl.trim()) {
        errors.push({ field: 'apiBaseUrl', message: '已启用整理但未填写 API Base URL。' });
      }
      return { ok: errors.length === 0, errors };
    },
    async saveSettingsDraft(next) {
      await delay(150);
      draft = structuredClone(next);
      return { ok: true };
    },
    async testLlmConnection(next) {
      await delay(800);
      const active = next?.llmPresets[next.selectedLlmPresetIndex ?? 0];
      if (!active?.apiBaseUrl.trim()) {
        return { ok: false, message: '失败：请先填写 API Base URL', elapsedMs: 800 };
      }
      return { ok: true, message: '成功（mock 800 ms，router confidence=0.92）', elapsedMs: 800 };
    },
    async openHotkeyCaptureDialog(currentHotkey: string) {
      await delay(200);
      void currentHotkey;
      const accepted = window.confirm(
        'Mock 热键录入：确定使用 Ctrl+Alt+Shift+Space？\n（真实环境由 OpenHotkeyCaptureDialog 打开原生对话框）',
      );
      return accepted
        ? { hotkey: 'Ctrl+Alt+Shift+Space', cancelled: false }
        : { hotkey: currentHotkey, cancelled: true };
    },
    async applyPttHotkey(hotkey: string) {
      await delay(50);
      draft.pttHotkey = hotkey;
      return { ok: true, activeHotkey: hotkey };
    },
    async openFolderPickerDialog(initialPath: string) {
      await delay(100);
      const next = window.prompt('Mock 选择目录（输入绝对路径）', initialPath || 'C:\\models');
      return next ? { path: next, cancelled: false } : { path: initialPath, cancelled: true };
    },
    async startEnrollmentUtterance() {
      await delay(100);
      return { ok: true };
    },
    async stopEnrollmentUtterance() {
      await delay(400);
      mockUtteranceCount += 1;
      return { durationMs: 3200, ok: true, utteranceCount: mockUtteranceCount };
    },
    async completeEnrollment(name: string, utteranceCount: number) {
      await delay(200);
      void utteranceCount;
      if (!name.trim()) {
        return { ok: false, error: '请输入姓名。' };
      }
      mockUtteranceCount = 0;
      return { ok: true, userId: 'mock-user' };
    },
    async getPrivacyConsentState(apiBaseUrl: string) {
      await delay(80);
      return {
        needsPrompt: true,
        host: 'api.openai.com',
        apiBaseUrl,
        isLoopback: false,
        message: '提示词整理将把识别文本发送到 api.openai.com。继续？',
      };
    },
    async acceptPrivacy(host: string) {
      await delay(50);
      void host;
      return { ok: true };
    },
    async listFeaturePresets() {
      await delay(40);
      return draft.featurePresets.map((p, index) => ({
        index,
        name: p.name,
        llmPresetName: p.llmPresetName,
        forcedIntent: p.forcedIntent,
        forcedSpecialistKey: resolveFeaturePresetStyleKey(p),
        onRefineFailure: p.onRefineFailure,
        optionalOverlaySkills: [...p.optionalOverlaySkills],
        selected: index === draft.selectedFeaturePresetIndex,
      }));
    },
    async applyFeaturePreset(index: number) {
      await delay(80);
      if (index < 0 || index >= draft.featurePresets.length) {
        return { ok: false, error: '功能预设索引无效。' };
      }
      draft.selectedFeaturePresetIndex = index;
      const fp = draft.featurePresets[index];
      const llmIdx = draft.llmPresets.findIndex(
        (p) => p.name.localeCompare(fp.llmPresetName, undefined, { sensitivity: 'accent' }) === 0,
      );
      if (llmIdx >= 0) {
        draft.selectedLlmPresetIndex = llmIdx;
      }
      draft.forcedIntent = fp.forcedIntent;
      draft.forcedSpecialistKey = fp.forcedSpecialistKey;
      draft.onRefineFailure = fp.onRefineFailure;
      draft.optionalOverlaySkills = [...fp.optionalOverlaySkills];
      draft.promptRefineEnabled = true;
      return { ok: true, selectedFeaturePresetIndex: index };
    },
    async requestClose(success: boolean) {
      void success;
      console.info('[bridge mock] requestClose');
    },
  };
}

let cachedBridge: AmrBridge | null = null;
let bridgeSource: 'host' | 'mock' = 'mock';

/** Whether the last getBridge() resolved to mock data. */
export function isMockBridge(): boolean {
  return bridgeSource === 'mock';
}

export function getBridgeSource(): 'host' | 'mock' {
  return bridgeSource;
}

/**
 * Returns the WebView2 host bridge when `hostObjects.amr` is registered,
 * otherwise a mock Promise-based implementation for Vite dev.
 */
export async function getBridge(): Promise<AmrBridge> {
  if (cachedBridge) {
    return cachedBridge;
  }

  const hostObjects = window.chrome?.webview?.hostObjects as
    | { amr?: AmrHostObject; sync?: { amr?: AmrHostObject } }
    | undefined;
  const host = hostObjects?.sync?.amr ?? hostObjects?.amr;
  if (host) {
    cachedBridge = wrapHostObject(host);
    bridgeSource = 'host';
    return cachedBridge;
  }

  // TODO(A4): remove mock fallback once WebUiBridge is wired in WebUiHostForm.
  console.warn('[bridge] hostObjects.amr not found — using mock bridge for development.');
  cachedBridge = createMockBridge();
  bridgeSource = 'mock';
  return cachedBridge;
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        hostObjects?: {
          amr?: AmrHostObject;
        };
        postMessage?: (message: string) => void;
      };
    };
  }
}
