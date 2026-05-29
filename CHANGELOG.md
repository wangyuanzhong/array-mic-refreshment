# Changelog

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
