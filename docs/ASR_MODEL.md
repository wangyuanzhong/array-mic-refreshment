# ASR 模型定稿（v0.1）

设置窗可选择已安装的 SenseVoice 包（2025-09 粤语优化 / 2024-07 int8 通用 / 2024-07 float32 高精度），未安装时可一键下载。

## 首版（已确认）

| 项 | 选择 |
|----|------|
| 运行时 | **Sherpa-ONNX** `OfflineRecognizer` |
| 模型 | **SenseVoice int8** |
| 主包 ID | `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09` |
| 回退 | `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17` |
| 模式 | 离线、非流式；**松开 PTT** 后整段识别 |

## 理由（简要）

- 与 C# + 句末 PTT 管线一致，CPU 与体积适合后台托盘。
- 中文/粤语场景成熟；首版不引入第二套 ASR 降低集成风险。

## 后续可选（首版不做 UI）

- **Qwen3-ASR-0.6B int8**：更高 CER，更慢；见 README「SenseVoice 和 Qwen3-ASR」对比。
- 实现仍通过 `IUtteranceAsr`，仅更换模型目录与 factory。

## 实现注意

- 设备原生采样率采集 → 模型边界 **16 kHz mono**。
- SenseVoice 输出若含情感/事件标签，管道只取 **纯文本** 字段。
- `download-models.ps1` / `ModelManifest` 仅包含 SenseVoice 条目（Phase 3）。
