# ASR 模型（Sherpa-ONNX 离线）

设置窗 **ASR 模型** 分区可选引擎；未安装的包可点击 **「下载模型」**（应用内从 GitHub release 下载到 `models/`）。

## 默认推荐（中英混说）

| 项 | 选择 |
|----|------|
| 引擎 | **FireRedASR2 CTC int8** |
| 包 ID | `sherpa-onnx-fire-red-asr2-ctc-zh_en-int8-2026-02-25` |
| 体积 | 约 740 MB |
| 场景 | 普通话 + **句内频繁夹英文** / code-switch |

## 可选 SenseVoice（纯中文或粤语为主）

| 包 ID | 说明 |
|-------|------|
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17` | int8 通用，有标点 |
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17` | float32 高精度，纯中文略优 |
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09` | 粤语优化 |

## 运行时

| 项 | 说明 |
|----|------|
| 栈 | **Sherpa-ONNX** `OfflineRecognizer` |
| 模式 | 离线、非流式；**松开 PTT** 后整段识别 |
| 音频 | 设备原生采样率 → 边界 **16 kHz mono** |
| SenseVoice | 输出去情感/事件标签（`SenseVoiceTextExtractor`） |
| FireRed CTC | 直接文本输出 |

## 下载方式

1. **应用内**：设置 → ASR 模型 → 选择包 → **下载模型**（目录默认 exe 旁 `models/`，可浏览修改）。
2. **脚本**：`.\scripts\download-models.ps1`（`-Package asr-primary` 默认 FireRed；`-Package all` 全部 ASR）。

清单与 URL：[`scripts/ModelManifest.json`](../scripts/ModelManifest.json)（嵌入 `ArrayMicRefreshment.Core` 供运行时下载）。

## 后续可选

- **Qwen3-ASR-0.6B int8**：更强多语言，体积与延迟更大；仍可通过 `IUtteranceAsr` + manifest 扩展。
