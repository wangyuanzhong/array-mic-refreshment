# Changelog

## V0.4.22 — 2026-05-26

### Fixed

- **Web HUD 只显示一条**：取消 Form DPI 缩放 + 覆盖 `#app { min-height: 100vh }`（嵌入 WebView 会把 100vh 当成整屏高度）；导航后注入 `innerHeight` 锁定布局；按 DPI 设置 `ZoomFactor`
- **唤醒指令结束时机**：改为「距最后一次语音活动（含轻声）满 `WakeCommandSilenceMs`」即结束；移除 VAD 在 600ms 时提前截断；环境噪声不再反复重置静音计时

## V0.4.21 — 2026-05-26

### Fixed

- **Web HUD**：浅色磨砂卡片、更大字号（Segoe UI Variable / 微软雅黑）、400×72 窗体、两行文案，解决裁切与深色条难看
- **唤醒词保存失败**：保存前校验 ppinyin 编码；内置词（小助手、小德小德、你好、蛋哥蛋哥）免 Python；改进 `py -3` / `sherpa-onnx-cli` 探测；设置页下拉与明确错误提示

## V0.4.20 — 2026-05-30

### Fixed

- **Web HUD 裁切**：HUD 窗体高度与 CSS 对齐（320×56），`ZoomFactor=1`，避免只显示上半条、文字不可见
- **唤醒词设置**：四项唤醒配置始终可编辑（不再因「仅 PTT」而 `disabled`）；保存后照常写入 settings 并应用
- **设置窗变小**：`AutoScaleMode.Dpi` + 将 `ClientSize` 持久化到 `settings.json`（`settingsWindowWidth/Height`）
- **声纹用户**：下拉首项固定为「无用户（不做声纹识别）」

## V0.4.19 — 2026-05-30

### Fixed

- **Web HUD 崩溃 (0x80010106)**：`VoiceWebStatusHud` 不再在 `Task.Run`（MTA）里初始化 WebView2；改为窗体 UI 线程 `await EnsureCoreWebView2Async`，启动时预初始化
- **纯 PTT 常开麦**：`keepStandbyCaptureBetweenSessions: false`；启动/模式切换/设置保存后 `StopStandbyListening`（仅按住热键时开麦）

### Verify

按住 PTT 不应再弹 JIT 对话框；日志无 `PTT standby capture started`（仅 PTT 模式）

## V0.4.18 — 2026-05-30

恢复 WebView 前已验证的 PTT 热键与采集路径（修复「按下变红、无松开、无后续」）。

### Fixed

- **PTT 热键**：托盘改回 `GlobalHotkeyListener`（`RegisterHotKey` + 同窗体 release 轮询），移除实机易卡死的 `LowLevelHotkeyHost` + 消息锚定窗
- **松开检测**：主键或组合修饰键抬起即结束 PTT；超 10 分钟自动松开防卡死
- **待机采集**：恢复 `keepStandbyCaptureBetweenSessions`（仅 PTT 模式）、启动 `warmTimer`、`SetVoiceTriggerMode(PttOnly)` 时 `StartStandbyListeningIfNeeded`
- **改热键无效**：设置页「点击录入」后调用 `ApplyPttHotkey` 立即注册并写入 `settings.json`；保存时展示 `Warning`（如未接托盘宿主）

### Verify

```powershell
.\scripts\watch-build-release.ps1 -Once
```

日志：`RegisterHotKey ok` → `chord down` → `PTT pressed` → **`chord released`** → `PTT released` → `Utterance ready`

## V0.4.17 — 2026-05-30

### Fixed

- **PTT 修饰键**：`LowLevelHotkeyHost` 在 UI 线程用 `GetAsyncKeyState` 校验 Ctrl/Alt 等（钩子线程内修饰键状态不可靠，日志常见 `modifiers mismatch`）
- **设置页唤醒词**：唤醒词区块始终显示（不再 `display:none`）；托盘已切唤醒模式时打开设置自动进入「触发与 HUD」并同步触发模式下拉框

### Verify

```powershell
.\scripts\watch-build-release.ps1 -Once
```

日志：`V0.4.17` → 仅 PTT 模式下 `chord down` / `PTT pressed`；仅唤醒词模式下 PTT 会提示未启用

## V0.4.16 — 2026-05-30

修正 V0.4.15 误用 `RegisterHotKey`（托盘 `ApplicationContext` 收不到 `WM_HOTKEY`）及恢复待机常开麦。

### Fixed

- **PTT 热键**：托盘改回 `LowLevelHotkeyHost` + 隐藏消息锚定窗；钩子回调经 `BeginInvoke` 投递 UI 线程（避免 `RunOnUiSync` 死锁）；松开以**主键抬起**为准
- **纯 PTT 常开麦**：`keepStandbyCaptureBetweenSessions: false`；启动/模式切换/设置保存后 `StopStandbyListening`；移除 `warmTimer` 预热待机

### Verify

```powershell
.\scripts\test-ptt-blackbox.ps1
.\scripts\watch-build-release.ps1 -Once
```

日志：`PTT low-level keyboard hook active` → 按住 `chord down` / `PTT pressed` → 松开 `chord released` / `PTT released`；启动后**不应**出现 `PTT standby capture started`

## V0.4.15 — 2026-05-30

恢复 WebView 改造前已验证的**全部后端运行时逻辑**（PTT / 唤醒 / 待机采集 / 模式切换 / 设置应用侧效应）；WebView 仅作 UI 壳，经 `WebUiBridge` + `SettingsApplyService` 接线。**保留**路线 B 新增：功能模式预设（模型 + Skill）、Web 设置/注册/HUD。

### Fixed

- **PTT 热键**：恢复 `GlobalHotkeyListener`（隐藏 Form + `RegisterHotKey`），移除 V0.4.12–14 低级键盘钩子（实机：有 `PTT pressed` 无 `PTT released` → UI 死锁、话筒卡住、崩溃）
- **`OnPttPressed`**：改回 `RunOnUi` + `IsPttHeld` 门控（`ec905dc`），禁止热键回调内 `RunOnUiSync`
- **启动 / 模式 / 设置后音频**：恢复 `warmTimer` → `WarmAudioCaptureIfNeeded`、`keepStandbyCaptureBetweenSessions`（仅 PTT）、`SetVoiceTriggerMode` 与 `RefreshAudioCaptureAfterSettings`（`ec905dc` 语义）
- **热键更新**：启动即 `RegisterHotKey`；设置保存仍走 `SettingsApplyService.TryUpdatePttHotkey`

### Unchanged (new since WebView)

- 功能模式预设（`FeaturePresetApplier`、托盘/Web 切换）
- Web 设置页、`WebUiBridge`、`SettingsApplyService` 替代原 `SettingsForm` 保存路径（侧效应与旧表单一致）

### Verify

```powershell
dotnet build ArrayMicRefreshment.sln -c Release
dotnet test ArrayMicRefreshment.sln -c Release --filter "FullyQualifiedName!~Integration"
.\scripts\test-phase2-route-b.ps1
.\scripts\test-feature-presets.ps1
.\scripts\test-ptt-blackbox.ps1
.\scripts\watch-build-release.ps1 -Once
```

## V0.4.14 — 2026-05-30

修复默认热键 **Ctrl+Alt+Space** 在实机无效；黑盒测试覆盖该组合。

### Fixed

- **Ctrl+Alt+Space**：钩子内跟踪 Ctrl/Alt/Shift/Win 状态 + `LLKHF_ALTDOWN` + 左右修饰键 VK；此前仅 `GetAsyncKeyState` 导致 Space 按下时修饰键判定失败
- 托盘：隐藏锚定窗体 + **UI Idle 后再注册钩子**（与 `Application.Run` 消息泵对齐）
- HUD：即使采集稍慢也先显示「录音中…」

### Added

- 黑盒：`Default_hotkey_CtrlAltSpace_*`、`ApplicationContext_pump_detects_CtrlAltSpace_like_tray_app`

### Verify

```powershell
.\scripts\test-ptt-blackbox.ps1
```

日志按住热键应有：`PTT hotkey chord down` → `PTT pressed` → `PTT capture ready`

## V0.4.13 — 2026-05-30

PTT 黑盒自动化测试；钩子安装使用当前进程模块句柄。

### Added

- `scripts/test-ptt-blackbox.ps1` + `PttHotkeyBlackBoxTests`：真实 `WH_KEYBOARD_LL` + `SendInput` 模拟 `Ctrl+Shift+F24`，验证按下/松开与 `PttCaptureService` 出 utterance
- 无需手按热键即可在本地 PowerShell 回归 PTT

### Fixed

- `LowLevelHotkeyHost`：`SetWindowsHookEx` 使用 `Process.MainModule` 模块句柄（替代 `GetModuleHandle(null)`）

### Verify

```powershell
.\scripts\test-ptt-blackbox.ps1
```

## V0.4.12 — 2026-05-30

修复托盘应用下 `RegisterHotKey` 注册成功但收不到 `WM_HOTKEY`、PTT 热键完全无响应的问题。

### Fixed

- **PTT 热键**：改用 `WH_KEYBOARD_LL` 低级键盘钩子（`LowLevelHotkeyHost`），不再依赖 `RegisterHotKey` / 隐藏窗体
- 日志：`PTT low-level keyboard hook active` → 按住时 `PTT hotkey chord down via keyboard hook` 与 `PTT pressed`

### Files / modules touched

- `LowLevelHotkeyHost.cs`、`NAudioPushToTalkSource.cs`；删除无效的 `PttHotkeyMessageForm.cs`

### Verify

- 仅 PTT + `Ctrl+Alt+Space`：按住应开麦、HUD「录音中」；日志有 hook 与 `PTT pressed`

## V0.4.11 — 2026-05-26

恢复「按住热键才开麦 + HUD 录音中」；Route B 下用消息窗体接收 WM_HOTKEY；纯 PTT 不再待机预开麦克风。

### Fixed

- **PTT 热键 + HUD**：新增 `PttHotkeyMessageForm`（WinForms 消息循环内 `RegisterHotKey`），替代仅 `NativeWindow` 的 `GlobalHotkeyListener`（V0.4.10 仍收不到 `PTT pressed`）
- **预开麦**：`keepStandbyCaptureBetweenSessions` 恒为 `false`；移除启动 `warmTimer`；切换/保存设置时 `StopStandbyListening`；仅按键时 `PttCaptureService` 才打开 WASAPI
- **HUD**：`OnPttPressed` 使用 `RunOnUiSync` 同步更新「录音中…」

### Files / modules touched

- `PttHotkeyMessageForm.cs`、`TrayApplicationContext.cs`、`IGlobalHotkeyHost.cs`
- `NAudioPushToTalkSource.cs`、`GlobalHotkeyListener.cs`

### Verify

- 仅 PTT：空闲时系统麦克风指示不应常亮；按住热键日志应有 `PTT hotkey WM_HOTKEY received` 与 `PTT pressed`；HUD 显示录音中
- 松开后进入识别流程

## V0.4.10 — 2026-05-29

恢复 Route B 之前可用的 PTT 热键路径；内置唤醒词 ppinyin 编码，修复「模型已安装但引擎未加载」。

### Fixed

- **PTT 热键**：移除 `HotkeySinkForm`，恢复 `NAudioPushToTalkSource` + `GlobalHotkeyListener`（与 `65bb718` 之前一致；日志显示 RegisterHotKey 成功但从未出现 `PTT pressed`）
- **KWS 加载**：日志根因为缺少 `wake-phrase-encodings.json` / Python `sherpa_onnx`；内置 `Resources/wake-phrase-encodings.json` 并在运行时复制到 KWS 目录
- `download-models.ps1 -IncludeKws` 后自动复制编码文件

### Files / modules touched

- `TrayApplicationContext.cs`、`WakeWordBuiltinEncodings.cs`、`WakeWordKeywordEncoder.cs`
- `Resources/wake-phrase-encodings.json`、`download-models.ps1`

### Verify

- 仅 PTT：按住热键日志应有 `PTT pressed`；设置页 KWS 为「已安装且引擎可加载」
- 唤醒模式：说出「小助手」可触发（非 stub）

## V0.4.9 — 2026-05-26

修复 PTT 热键 HWND 未创建导致注册无效；纯 PTT 在热键未注册时不预开麦克风；KWS「已安装」与引擎加载失败区分展示。

### Fixed

- `HotkeySinkForm.EnsureHandleReady()`：在 `RegisterHotKey` 前强制创建句柄（不可见窗体 `Show()` 不会创建 HWND）
- 启动 `Application.Idle` 与 `EnsurePttHotkeyRegistered` 重试注册；注册成功后再开启 PTT 待机采集
- 仅 PTT：热键未注册时不 `StartStandbyListening`（避免未按键时系统显示麦克风占用/像在开录）
- 唤醒模型：文件存在但 Sherpa 加载失败时不再报「未找到模型」；设置页增加 `engineReady` 状态

### Files / modules touched

- `HotkeySinkForm.cs`、`TrayApplicationContext.cs`、`WakeWordDetectorFactory.cs`
- `SettingsMetadataProvider.cs`、`WebUiBridge.Settings.cs`
- `ui/src/pages/SettingsPage.ts`、`ui/src/bridge.ts`

### Verify

- 仅 PTT：未按热键时托盘为就绪、系统麦克风指示不应常亮；按住已注册热键可录音
- 设置 → 触发与 HUD：KWS 显示「已安装且引擎可加载」或「文件已就绪，引擎未能加载」

## V0.4.8 — 2026-05-26

修复 PTT 全局热键在托盘消息循环下不触发；纯 PTT 模式仍监听唤醒；设置页 models/skills 目录可浏览选择。

### Fixed

- `HotkeySinkForm`：隐藏窗体接收 `WM_HOTKEY`（替代仅 `NativeWindow`，修复 PTT 热键无效）
- `ApplyWakeListeningForCurrentMode`：仅 PTT 时强制 `StopListening`；保存/切换触发模式后同步
- 保存设置后 `EnsurePttHotkeyRegistered` 重试注册热键

### Added

- Bridge `OpenFolderPickerDialog` + 设置页「浏览…」（模型目录、Skills 目录）

### Files / modules touched

- `HotkeySinkForm.cs`、`IGlobalHotkeyHost.cs`、`NAudioPushToTalkSource.cs`、`TrayApplicationContext.cs`
- `FolderPickerDialog.cs`、`WebUiBridge.Settings.cs`、`SettingsApplyService.cs`
- `ui/src/pages/SettingsPage.ts`、`ui/src/bridge.ts`

### Verify

- 仅 PTT + 保存：托盘无唤醒监听；按住热键可录音
- 设置 → ASR/路径 →「浏览…」选目录并保存

## V0.4.7 — 2026-05-26

修复热键录入对话框、PTT 热键重注册、触发模式与功能预设混淆；模型/skills 目录持久化为绝对路径；设置窗口单例。

### Fixed

- `HotkeyCaptureDialog`：加大 DPI 布局、表单级 `KeyPreview` 捕获；录入后自动确认
- PTT：设置保存或启动时若热键未注册则强制 `TryUpdateHotkey` 重试
- 触发模式：设置页展示 **持久化** `TriggerMode`（不再用运行时覆盖）；保存后同步 orchestrator；仅 PTT 时显式 `StopListening`
- 功能预设：文案标明不改变 PTT/唤醒（与「触发模式」分离）
- 设置：`WebUiHostForm` `HideOnClose` 单例，重复打开复用同一窗口
- 托盘：移除「注册说话人…」（注册仍在设置 → 说话人 或 `#/enroll` 路由）
- `SettingsPathNormalizer`：`models` / `skills` 保存为 exe 可解析的绝对路径

### Files / modules touched

- `HotkeyCaptureDialog.cs`、`HotkeyCaptureTextBox.cs`、`TrayApplicationContext.cs`、`SettingsApplyService.cs`
- `WebUiHostForm.cs`、`WebUiBridge.cs`、`SettingsDraftMapper.cs`、`SettingsPathNormalizer.cs`
- `SkillsPathResolver.cs`、`ui/src/pages/SettingsPage.ts`

### Verify

- 托盘 → 设置 →「点击录入」热键：对话框完整可见、按键可录入
- 仅 PTT 模式：无唤醒监听；热键可录音
- 多次打开设置：只有一个窗口
- `.\scripts\watch-build-release.ps1 -Once`

## V0.4.6 — 2026-05-26

修复 Web 设置页分区叠在一起只显示标题；在 **触发与 HUD** 展示 KWS 模型就绪状态；Web HUD 延迟初始化 WebView2 避免 UI 线程死锁。

### Fixed

- `SettingsPage`：左侧 Nav 切换时仅渲染当前 `settings-section`（`is-active`）
- `GetWakeWordModelStatus` bridge：设置页显示 KWS 目录与 `installed` 状态（无单独「唤醒模型」导航项）
- `VoiceWebStatusHud`：首次 `Show` 再初始化 WebView2，后台等待 `EnsureCoreWebView2Async`，避免同步阻塞 UI 线程
- `watch-build-release.ps1`：修正 `function Name()` 语法、`-Once` 调用 `build-release.ps1` 参数传递、去掉易触发解析错误的 Unicode 符号

### Files / modules touched

- `ui/src/pages/SettingsPage.ts`、`ui/src/styles/components.css`、`ui/src/bridge.ts`
- `WebUiBridge.Settings.cs`、`SettingsMetadataProvider.cs`、`IWebUiBridge.cs`
- `VoiceWebStatusHud.cs`、`scripts/watch-build-release.ps1`
- `docs/UI_ROUTE_B_WEBVIEW2.md`、`docs/LOCAL_DEVELOPMENT.md`

### Verify

- 托盘 → 设置：点击左侧各分区应只显示对应表单；**触发与 HUD** 可见 KWS 状态
- `.\scripts\watch-build-release.ps1 -Once` → `dist\ArrayMicRefreshment-self-contained\ArrayMicRefreshment.exe`

## V0.4.5 — 2026-05-29

同步 [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule) **0.9.1**（`ce5dad6`），强化 Agent **主动输出 Done check**（计划须含 `Closing:` 承诺行）。无应用运行时变更。

### Changed

- `.cursor/rules/00-universal-core.mdc` — 新增 “The single most important rule”；MODE 检测改为计划两行；禁止等用户追问才补 Done check
- `AGENTS.md`、`.cursor/README.md`、`.cursor/UNIVERSAL_RULE_LOCK` 对齐 0.9.1

### Files / modules touched

- `.cursor/rules/*.mdc`、`.cursor/UNIVERSAL_RULE_LOCK`、`scripts/sync-universal-cursor-rules.ps1`
- `AGENTS.md`、`.cursor/README.md`、`CHANGELOG.md`、`VERSION.txt`、`AppInfo.cs`、`ArrayMicRefreshment.App.csproj`

### Verify

- `UNIVERSAL_RULE_LOCK` → `ce5dad6` / `0.9.1`
- 新会话 Agent 计划应含 `Closing: I will end this reply with the verbatim Done check.`

## V0.4.4 — 2026-05-29

路线 B Phase 4 可选项：**透明 WebView2 状态 HUD**（`#/hud`），与 Macaron token 一致；无法初始化时自动回退原生 `VoiceStatusHud`。

### Added

- `VoiceWebStatusHud` + `VoiceStatusHudFactory`：`WS_EX_NOACTIVATE` / `ShowWithoutActivation`，C# `PostWebMessageAsJson` 驱动 `ui/#/hud`
- 设置页「使用 WebView2 状态 HUD（实验）」；`AppSettings.UseWebStatusHud`（默认开，**重启应用**生效）
- 环境变量 `AMR_WEB_HUD=0|1` 覆盖设置
- `VoiceStatusHudFactoryTests`

### Changed

- `VoiceFeedbackPresenter` 经工厂选择 Web / 原生 HUD
- `docs/UI_ROUTE_B_WEBVIEW2.md` §16 Phase 4 Web HUD 勾选完成

### Files / modules touched

- `src/ArrayMicRefreshment.App/VoiceWebStatusHud.cs`、`VoiceStatusHudFactory.cs`、`IVoiceStatusHud.cs`、`VoiceFeedbackPresenter.cs`
- `ui/src/pages/HudPage.ts`、`router.ts`、`components.css`
- `AppSettings.cs`、`SettingsDraft*`、`SettingsPage.ts`、`bridge.ts`
- `tests/ArrayMicRefreshment.App.Tests/VoiceStatusHudFactoryTests.cs`

### Verify

- Windows：`AMR_WEB_HUD=1` 且已 `npm run build` → PTT/唤醒时见 Web 条；`AMR_WEB_HUD=0` → 原生条
- §10.2：确认 HUD **不抢焦点**、粘贴仍成功
- `dotnet test` App.Tests 含 `VoiceStatusHudFactoryTests`

## V0.4.3 — 2026-05-29

迁移 [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule) **0.9.0**（`a121a4b`）：规则单目录同步，移除重复的 `github-actions-ci` skill。

### Added

- `post-push-ci-green.mdc` 本仓库段：workflow 表、常见 CI 坑、本地 pre-push 命令（原 skill 内容）

### Changed

- `.cursor/rules/*.mdc` 自 universal **0.9.0** 刷新（含 change-impact grep sweep、「每个 agent 跑完整规则」）
- `scripts/sync-universal-cursor-rules.ps1`：按 README 仅 `cp rules/*.mdc`，不再调用已删除的安装脚本
- `apply-amr-cursor-overlays.ps1`：CI 指引改指向 `post-push-ci-green.mdc`
- `AGENTS.md`、`.cursor/README.md`、`docs/LOCAL_DEVELOPMENT.md` 与 0.9.0 对齐

### Removed

- `.cursor/skills/github-actions-ci/`（上游 0.9.0 已删除；内容并入规则）

### Files / modules touched

- `.cursor/rules/`、`.cursor/UNIVERSAL_RULE_LOCK`、`scripts/sync-universal-cursor-rules.ps1`、`scripts/apply-amr-cursor-overlays.ps1`
- `AGENTS.md`、`.cursor/README.md`、`docs/LOCAL_DEVELOPMENT.md`、`CHANGELOG.md`、`VERSION.txt`、`AppInfo.cs`、`ArrayMicRefreshment.App.csproj`

### Verify

- `.cursor/UNIVERSAL_RULE_LOCK` 含 `universal-pack-version=0.9.0` 与 `a121a4b`
- `grep -R github-actions-ci .cursor/rules` 无 SKILL 引用
- `test ! -d .cursor/skills/github-actions-ci`

## V0.4.2 — 2026-05-29

同步升级后的 [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule)（`96e3c5d`），强化 Agent 收尾契约，不改变应用运行时行为。

### Added

- 通用规则 `local-auto-push-current-branch.mdc`（须 `.cursor/.local-auto-push` 标记才在 Local 模式自动 push）
- `00-universal-core`：强制 `MODE:` 声明、verbatim **Done check**、子 Agent push/CI/CHANGELOG 验证

### Changed

- `docs-sync-before-finish`：禁止「Reviewed N docs」聚合写法，要求逐文件 `Docs review:` 枚举
- `post-push-ci-green`：明确「谁 push 谁 watch」；子 Agent 不能 push 后甩锅给父 Agent
- `apply-amr-cursor-overlays.ps1` 改为**仅追加** AMR 专项段，避免覆盖弱化新版通用正文
- `AGENTS.md`、`.cursor/README.md` 与新版规则对齐

### Files / modules touched

- `.cursor/rules/*.mdc` — 自 universal `96e3c5d` 刷新 + AMR 追加段
- `scripts/apply-amr-cursor-overlays.ps1`、`scripts/sync-universal-cursor-rules.ps1`
- `AGENTS.md`、`.cursor/README.md`、`.cursor/UNIVERSAL_RULE_LOCK`

### Verify

- 对比 `.cursor/UNIVERSAL_RULE_LOCK` 与 universal 仓库 `main` HEAD
- 新开 Agent 任务应输出 `MODE:` 行与完整 Done check

## V0.4.1 — 2026-05-28

本次 push 修复 Windows CI「假卡死」与规则收尾缺口，不改变音频/唤醒/ASR 管道行为。

### Fixed

- **Windows CI `App.Tests` 挂死**：无窗体时 `PrivacyConsent` 不再调 `MessageBox.Show`；单元测试不再构造 `NAudioPushToTalkSource`（避免 WinForms `Timer` 泄漏）
- **`SettingsDraftMapper`**：保存 draft 时将顶层整理字段同步进当前功能预设，与 Web UI 编辑一致
- **Phase2 / WebUiBridge 测试**：功能预设与 LLM 预设改名后的校验对齐

### Changed

- CI：`build-windows` 增加 `timeout-minutes: 30`、`--blame-hang-timeout 2m`、`VSTEST_TESTCASE_TIMEOUT`
- 文档：`AGENTS.md`、`docs/UI_ROUTE_B_WEBVIEW2.md`、`README.md` 与 V0.4 产品事实对齐；废弃 `.cursor/.local-skip-post-push-ci` 说明

### Files / modules touched

- `src/ArrayMicRefreshment.App/PrivacyConsent.cs` — headless 不弹隐私框
- `src/ArrayMicRefreshment.App/Web/SettingsDraftMapper.cs` — 功能预设与 draft 同步
- `tests/ArrayMicRefreshment.App.Tests/` — Phase2 / WebUiBridge 测试
- `.github/workflows/ci.yml` — Windows 测试超时与 hang 诊断
- `AGENTS.md`、`.cursor/rules/00-universal-core.mdc`、`.cursor/README.md` — post-push CI 无跳过

### Verify

- `gh run watch` 至 `build-windows` + `build-and-test` success（例：run `26602916264`）
- Windows：`.\scripts\test-phase2-route-b.ps1` 与 `.\scripts\test-feature-presets.ps1`

## V0.4 — 2026-05-28

### Added
- **功能预设**：LLM 预设名称 + 整理风格/叠加 skill 组合；设置页「功能预设」分区；托盘右键「功能模式」快速切换
- **WebView2 设置 UI**（路线 B）：托盘「设置」打开 PWA 风格 Web 页（侧栏导航 + 卡片布局），含设备、ASR、LLM 预设、唤醒词、热键等完整字段
- **`scripts/test-feature-presets.ps1`**：功能预设与 Phase 2 验收自动化（Windows App.Tests）
- **`SettingsApplyService`**：从 `TrayApplicationContext` 抽出设置保存/应用逻辑，Web Bridge 共用
- **`ui/` 前端工程**：Vite + TypeScript；Release 前需 `npm run build` 产出 `wwwroot/`（见 [`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md)）
- Web 路由：`#/settings`、`#/enroll`、`#/privacy`
- **`DesignTokens`**：原生 `VoiceStatusHud` 与 `ui/src/styles/tokens.css` Macaron 配色对齐

### Changed
- 原「提示词整理」设置区更名为 **功能预设**（多预设新建/删除/下拉选择）
- 设置页布局：仅右侧内容区滚动，侧栏与分区导航固定（减少滚动时全页抖动/发糊）
- 设置界面视觉：**Macaron 马卡龙 Pastel** 设计系统（`ui/src/styles/tokens.css`）
- 打包脚本 `build-release.ps1` 在 publish 前构建前端
- 移除 legacy WinForms `SettingsForm` / `EnrollmentDialog`；设置与注册统一 WebView2

### Removed
- `SettingsForm.cs`、`EnrollmentDialog.cs` 及 `AMR_USE_WINFORMS_SETTINGS` 环境变量降级

### Notes
- **无音频/唤醒/ASR 管道逻辑变更**；麦克风 → 识别 → 输出链路行为与 V0.3 一致
- 需安装 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 10/11 通常已自带 Evergreen）

## V0.3 — 2026-05-26

### Added
- 开机自启设置（默认开启，写入 HKCU Run 注册表）
- PTT 独占模式 standby 常开麦克风 + ~0.9s rolling pre-roll，按下即录、减少丢字

### Fixed
- PTT 热键 HUD 延迟：UI 反馈优先于 capture 启动
- PTT 丢字头：撤销异步开麦，Both 模式先取 wake pre-roll 再释放唤醒 mic
- 唤醒指令结束静音：按连续低于语音阈值计时，配置 ms 与体感一致
- HUD 抢焦点导致无法粘贴；唤醒时提前锁定粘贴目标
- PTT + 唤醒词模式 pre-roll 交接，减少按 PTT 后开头丢字
- 设置窗唤醒词区块布局错位与隐藏后留白

### Changed
- 设置窗移除唤醒词下方冗余说明；「开机自启」独立一行
- PTT warm 仅 PttOnly 模式，启动后 100ms 开启 standby

## V0.2 — 2026-05-26

### Added
- Sherpa-ONNX 唤醒词检测（Zipformer KWS）与唤醒后指令采集
- 唤醒成功/识别完成托盘气泡反馈
- 唤醒词编码生成脚本与打包脚本（`generate-wake-encodings.ps1`、`pack-ready.ps1`）

### Fixed
- 唤醒后 dictation 被环境噪声续命导致 20–30s 才提交 ASR；收紧语音结束检测与最长指令时长
- 保存设置时打断唤醒会话、不必要的麦克风重启
- 唤醒流程跳过声纹门禁（唤醒本身已证明麦克风有效）

### Changed
- 唤醒后静音结束 1.2s → 0.9s，最长指令 30s → 12s，回声忽略 450ms

## v0.1.1 — 2026-05-25

### Fixed
- 语音整理成功/失败改为依据 LLM 是否返回非空文本（与 ASR 原文相同也算成功）
- 纯文本整理使用短 prompt（`plain-text-polish.md`）并附带 `/no_think`（Qwen3 / LM Studio）

### Build
- 重新打包 `dist\ArrayMicRefreshment-self-contained` 与 `dist\ArrayMicRefreshment-ready-new`（含最新 skills + models）

## v0.1 — 2026-05-24

### Added
- 设置窗 **LLM 三预设**（API URL / Key / Model 可切换）
- **纯文本整理** 技能（去口误、加标点，内置 prompt）
- 设置内 **测试 API 连接**
- 完整离线包构建：`dist\ArrayMicRefreshment-ready.zip`（exe + models + skills）
- ASR 模型下载与多模型选择（SenseVoice 2024/2025）
- 声纹门禁、PTT 热键捕获、自动粘贴到光标

### Fixed
- 设置「确定 / 测试连接」导致程序崩溃
- 设置窗高度不足，测试结果行被遮挡
- PTT 松开优先触发、热键误报气泡、说话人识别阈值与性能
