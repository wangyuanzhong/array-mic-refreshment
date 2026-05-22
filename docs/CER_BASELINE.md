# CER Baseline — SenseVoice int8 (Phase 5)

Measured on **2026-05-22** with `scripts/Measure-Cer.ps1` / `scripts/CerMeasure`.
Model: `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09` under `models/`.

| # | Lang | Reference | Recognized | CER % |
|---|------|-----------|------------|-------|
| 1 | zh-en-mix | ApiService 调用 async await 方法 | AP SURVICE 调用 A信 方法 | 67.9 |
| 2 | zh | 请帮我创建一个 React 按钮 | 请帮我创建一个 REACT 按钮 | 25.0 |
| 3 | en | git checkout main and pull latest | GET CHECK OUT MAIN AND PUL LATDEST | 90.9 |
| 4 | zh-en-mix | useEffect 依赖数组要写完整 | E EFFECT 依赖数组要写完整 | 44.4 |
| 5 | en | TypeScript interface vs type alias | TYPE SCRIPT INTERFCE V S TYPE ALIUS | 88.2 |
| 6 | zh-en-mix | 请检查 null reference exception | 请检查 NO REFERENCE EXCCCEPTION | 85.7 |
| 7 | en | Open the settings dialog and save | OPEN THE SETTING DILOG AND SAFE | 81.8 |
| 8 | zh-en-mix | 数据库迁移用 migration 命令 | 数据库迁移用 MIGRATION 命令 | 47.4 |
| 9 | en | this dot props dot children | HIS DOC PROPS DOC CHILDREN | 85.2 |
| 10 | zh-en-mix | 异步 promise then catch finally | HE WILL PROMISE THEN CATSH FINALLY | 103.4 |

**Mean CER:** 72.0%
**Code-term subset mean CER:** 72.8% (6 utterances)

## Conclusion
Code-mixed / programming terminology shows elevated CER (>25%). README future-work suggests evaluating **Qwen3-ASR** for code-heavy dictation.
