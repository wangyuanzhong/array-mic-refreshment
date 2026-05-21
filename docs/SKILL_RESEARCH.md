# Skill 调研：意图路由 + 多 Skill 协同

> 更新：2026-05。产品策略：**根据用户说话内容选 Skill**，而非默认代码向。

---

## 业界模式

| 项目 | 模式 | 与本产品关系 |
|------|------|----------------|
| [shanttoosh/voice-controlled-ai-agent](https://github.com/shanttoosh/voice-controlled-ai-agent) | STT → **LLM 意图** → LangGraph → 不同 tool | ✅ 同款「先分类再处理」 |
| [Intent_IQ](https://github.com/sagar31joon/Intent_IQ) | embedding/分类 → **动态 skill 模块** | ✅ 模块化 skill 文件 |
| [danielrosehill/Speech-To-Text-System-Prompt-Library](https://github.com/danielrosehill/Speech-To-Text-System-Prompt-Library) | **basic cleanup + 叠 layer**（邮件/任务/AI prompt…） | ✅ Specialist 来源库 |
| [FlorianBruniaux/.../voice-refine](https://github.com/FlorianBruniaux/claude-code-ultimate-guide/blob/main/examples/skills/voice-refine/SKILL.md) | 单 Skill 压 token + 结构化 Markdown | ⚠ 不默认；偏 Claude Code 长任务 |

---

## 本仓库结构

```text
skills/router/SKILL.md          # LLM #1：只输出 JSON intent
skills/shared/stt-base.md       # 共用 STT 清理片段
skills/intents/*/SKILL.md       # LLM #2：按意图整理成一条文本
```

详见 [`SKILL_PIPELINE.md`](SKILL_PIPELINE.md)、[`skills/README.md`](../skills/README.md)。

---

## Specialist 与上游

| Intent | 上游 prompt | 场景 |
|--------|-------------|------|
| `code-editing` | [code-editing.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/development/code-editing.md) | 用户明显在讲编程 |
| `general-ai` | [general-prompt.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/general-prompt.md) | 通用问答/写作 |
| `research` | [deep-research-prompt.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/deep-research-prompt.md) | 调研/对比 |
| `task-plan` | [to-do-list.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/to-do-list.md) | 待办/步骤 |

共用：[Voice-Prompt-Enhancement-Node](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node)、[STT-Basic-Cleanup](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt)。

---

## 为何不用「单 Skill 全包」

- 用户说调研时，代码向 prompt 会改坏语气与结构。
- 用户说改代码时，general prompt 不够实现向。
- Router + Specialist 可 **独立迭代**、设置页可 **强制意图** 跳过 Router。
