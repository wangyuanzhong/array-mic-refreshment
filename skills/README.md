# Skills 目录 — 意图路由 + 多 Skill 协同

提示词整理**不以代码为默认**，而是根据 **ASR 原文内容判断用户在干什么**，再选用对应 Skill 做二次整理。

## 流程（启用「提示词整理」时）

```text
SenseVoice 原文
    → ① Router Skill（LLM 调用 #1，只输出 JSON 意图）
    → ② 加载 intents/{intent}/SKILL.md
    → ③ Specialist Skill（LLM 调用 #2，输出一条可粘贴文本）
    → 剪贴板
```

参考业界做法：[voice-controlled-ai-agent](https://github.com/shanttoosh/voice-controlled-ai-agent)（Whisper → **意图分类** → 不同 tool）、[danielrosehill 的 Prompt 栈](https://github.com/danielrosehill/Speech-To-Text-System-Prompt-Library)（basic cleanup + 按场景叠 layer）。

## 目录结构

| 路径 | 作用 |
|------|------|
| [`router/SKILL.md`](router/SKILL.md) | **意图识别**：`code-editing` / `general-ai` / `research` / `task-plan` |
| [`shared/stt-base.md`](shared/stt-base.md) | 各 Specialist 共用的 STT 清理要点（嵌入 system 或拼接） |
| [`intents/code-editing/SKILL.md`](intents/code-editing/SKILL.md) | 编程、改代码、IDE、API、框架、refactor… |
| [`intents/general-ai/SKILL.md`](intents/general-ai/SKILL.md) | 通用聊天、问答、写作、闲聊指令 |
| [`intents/research/SKILL.md`](intents/research/SKILL.md) | 调研、对比、深度检索类问题 |
| [`intents/task-plan/SKILL.md`](intents/task-plan/SKILL.md) | 待办、清单、步骤、日程类 |

## 设置页（规划）

| 选项 | 说明 |
|------|------|
| **自动（默认）** | 走 Router |
| **强制：编程 / 通用 / 调研 / 待办** | 跳过 Router，调试或用户自知场景 |
| Router / Specialist 模型 | 可与整理 API 相同；Router 建议用小模型、低 temperature |

## C# 契约（Phase 4）

```csharp
enum PromptIntent { Auto, CodeEditing, GeneralAi, Research, TaskPlan }

record IntentResult(PromptIntent Intent, float Confidence, string? Reason);

interface IIntentRouter {
    Task<IntentResult> RouteAsync(string rawTranscript, CancellationToken ct);
}

interface IPromptRefiner {
    Task<string> RefineAsync(string rawTranscript, PromptIntent intent, CancellationToken ct);
}
```

实现：`RouteAsync` 读 `skills/router/SKILL.md`，解析 JSON；`RefineAsync` 读 `skills/intents/{intent}/SKILL.md` + 拼接 `shared/stt-base.md`。

## 文档

- [`docs/SKILL_PIPELINE.md`](../docs/SKILL_PIPELINE.md) — 管线与判据详解
- [`docs/SKILL_RESEARCH.md`](../docs/SKILL_RESEARCH.md) — 上游仓库调研
