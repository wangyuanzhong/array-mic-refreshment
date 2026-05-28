# Changelog

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
