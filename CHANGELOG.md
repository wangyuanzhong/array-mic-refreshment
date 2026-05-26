# Changelog

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
