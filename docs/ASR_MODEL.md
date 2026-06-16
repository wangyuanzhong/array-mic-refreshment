# ASR 模型（Sherpa-ONNX 离线）

设置窗 **ASR 模型** 分区可选引擎；未安装的包可点击 **「下载模型」**（应用内从 GitHub release 下载到 `models/`）。

## 默认推荐（中英混说）

| 项 | 选择 |
|----|------|
| 引擎 | **Qwen3-ASR 0.6B int8** |
| 包 ID | `sherpa-onnx-qwen3-asr-0.6B-int8-2026-03-25` |
| 体积 | 约 940 MB |
| 场景 | 普通话 + **句内频繁夹英文** / code-switch；离线非流式 PTT |

## 备选 FireRed / SenseVoice

| 包 ID | 说明 |
|-------|------|
| `sherpa-onnx-fire-red-asr2-ctc-zh_en-int8-2026-02-25` | FireRed CTC int8，中英混说，约 740 MB |
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17` | int8 通用，有标点 |
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17` | float32 高精度，纯中文略优 |
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09` | 粤语优化 |

## 运行时

| 项 | 说明 |
|----|------|
| 栈 | **Sherpa-ONNX** `OfflineRecognizer` |
| 模式 | 离线、非流式；**松开 PTT** 后整段识别 |
| 音频 | 设备原生采样率 → 边界 **16 kHz mono** |
| Qwen3-ASR | `OfflineQwen3AsrModelConfig`；`max_new_tokens` 512；CPU 线程 `min(8, max(2, cores/2))` |
| SenseVoice | 输出去情感/事件标签（`SenseVoiceTextExtractor`） |
| FireRed CTC | 直接文本输出 |

## 下载方式

1. **应用内**：设置 → ASR 模型 → 选择包 → **下载模型**（目录默认 exe 旁 `models/`，可浏览修改）。
2. **脚本**：`.\scripts\download-models.ps1`（`-Package all` 含 Qwen3 + FireRed + SenseVoice；`-Package asr-primary` 为 manifest 主包）。

清单与 URL：[`scripts/ModelManifest.json`](../scripts/ModelManifest.json)（嵌入 `ArrayMicRefreshment.Core` 供运行时下载）。

## 纯文本整理与中英混说

ASR 原文进入 **纯文本整理** 时：无拉丁字母 → 原 prompt；含英文 → 自动混说 prompt + 英文 token 保护，整理失败回退 ASR 原文。见 [`docs/SKILL_PIPELINE.md`](SKILL_PIPELINE.md)。
