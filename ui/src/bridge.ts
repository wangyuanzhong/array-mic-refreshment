/** Settings draft DTO — mirrors docs/UI_ROUTE_B_WEBVIEW2.md §7.3 and AppSettings. */
export type ForcedIntent =
  | 'Auto'
  | 'PlainText'
  | 'GeneralAi'
  | 'CodeEditing'
  | 'Research'
  | 'TaskPlan';

export type OnRefineFailure = 'UseRawTranscript' | 'ShowError' | 'KeepLast';

export type TriggerMode = 'PttOnly' | 'WakeWordOnly' | 'Both';

export type WakeWordSensitivity = 'Standard' | 'High' | 'Maximum';

export type HudScreenCorner = 'BottomRight' | 'BottomLeft' | 'TopRight' | 'TopLeft';

export interface LlmPresetDraft {
  name: string;
  apiBaseUrl: string;
  apiKey: string;
  apiModel: string;
}

export interface SettingsDraft {
  masterEnabled: boolean;
  pasteToCaretEnabled: boolean;
  launchAtStartup: boolean;
  promptRefineEnabled: boolean;
  forcedIntent: ForcedIntent;
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
  pttHotkey: string;

  selectedLlmPresetIndex: number;
  llmPresets: LlmPresetDraft[];
  optionalOverlaySkills: string[];
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

export interface OptionalOverlaySkillItem {
  key: string;
  label: string;
  checked: boolean;
}

export interface SkillsCatalogStatus {
  missingFiles: string[];
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

/** Raw COM host object shape (WebView2 async proxy). */
export interface AmrHostObject {
  GetAppInfo(): Promise<string>;
  GetRuntimeState(): Promise<string>;
  ListAudioDevices(): Promise<string>;
  ListSpeakerUsers(): Promise<string>;
  ListAsrModels(): Promise<string>;
  ListOptionalOverlaySkills(skillsDirectory: string): Promise<string>;
  GetSkillsCatalogStatus(skillsDirectory: string): Promise<string>;
  LoadSettingsDraft(): Promise<string>;
  ValidateSettingsDraft(draftJson: string): Promise<string>;
  SaveSettingsDraft(draftJson: string): Promise<string>;
  TestLlmConnection(draftJson?: string): Promise<string>;
  OpenHotkeyCaptureDialog(currentHotkey: string): Promise<string>;
  StartEnrollmentUtterance(): Promise<string>;
  StopEnrollmentUtterance(): Promise<string>;
  CompleteEnrollment(name: string, utteranceCount: number): Promise<string>;
  GetPrivacyConsentState(apiBaseUrl: string): Promise<string>;
  AcceptPrivacy(host: string): Promise<string>;
  RequestClose(success: boolean): Promise<void>;
}

export interface AmrBridge {
  getAppInfo(): Promise<AppInfo>;
  getRuntimeState(): Promise<RuntimeState>;
  listAudioDevices(): Promise<AudioDeviceItem[]>;
  listSpeakerUsers(): Promise<SpeakerUserItem[]>;
  listAsrModels(): Promise<AsrModelItem[]>;
  listOptionalOverlaySkills(skillsDirectory: string): Promise<OptionalOverlaySkillItem[]>;
  getSkillsCatalogStatus(skillsDirectory: string): Promise<SkillsCatalogStatus>;
  loadSettingsDraft(): Promise<SettingsDraft>;
  validateSettingsDraft(draft: SettingsDraft): Promise<ValidateResult>;
  saveSettingsDraft(draft: SettingsDraft): Promise<SaveResult>;
  testLlmConnection(draft?: SettingsDraft): Promise<TestLlmResult>;
  openHotkeyCaptureDialog(currentHotkey: string): Promise<HotkeyCaptureResult>;
  startEnrollmentUtterance(): Promise<StartEnrollmentResult>;
  stopEnrollmentUtterance(): Promise<StopEnrollmentResult>;
  completeEnrollment(name: string, utteranceCount: number): Promise<CompleteEnrollmentResult>;
  getPrivacyConsentState(apiBaseUrl: string): Promise<PrivacyConsentState>;
  acceptPrivacy(host: string): Promise<AcceptPrivacyResult>;
  requestClose(success: boolean): Promise<void>;
}

export function createDefaultSettingsDraft(): SettingsDraft {
  return {
    masterEnabled: true,
    pasteToCaretEnabled: true,
    launchAtStartup: true,
    promptRefineEnabled: false,
    forcedIntent: 'PlainText',
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
    wakeCommandSilenceMs: 3000,
    wakeUseVadEndDetection: true,
    hudScreenCorner: 'BottomRight',
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
  };
}

function parseJson<T>(raw: string, label: string): T {
  try {
    return JSON.parse(raw) as T;
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
        { id: '', displayName: '(无)', isNone: true },
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
      // TODO(A4): replace with native HotkeyCaptureDialog via WebUiBridge.
      const accepted = window.confirm(
        'Mock 热键录入：确定使用 Ctrl+Alt+Shift+Space？\n（真实环境由 OpenHotkeyCaptureDialog 打开原生对话框）',
      );
      return accepted
        ? { hotkey: 'Ctrl+Alt+Shift+Space', cancelled: false }
        : { hotkey: currentHotkey, cancelled: true };
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

  const host = window.chrome?.webview?.hostObjects?.amr as AmrHostObject | undefined;
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
