# Changelog

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
