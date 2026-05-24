# Changelog

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
