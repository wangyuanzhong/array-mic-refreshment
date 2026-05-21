# 多 Skill 协同管线

## 设计原则

1. **内容决定意图**：用户在说编程、查资料、列待办还是普通提问，应从 **ASR 文本** 判断，而不是默认当成写代码。
2. **两次 LLM 调用**（可配置同一 endpoint）：
   - **Router**：极短输出，降低误判成本；
   - **Specialist**：按意图用不同 system prompt（源自 danielrosehill 等开源 prompt）。
3. **输出统一**：仍为 **一条** 可粘贴进 AI 聊天框的文本（`task-plan` 可用分号分隔多任务，避免 Markdown 大标题）。

## 意图定义与判据

| intent | 何时选 | 典型口语信号 |
|--------|--------|----------------|
| `code-editing` | 改代码、实现功能、调试、框架/API/文件/类/函数 | 「组件」「接口」「async」「refactor」「这个函数」「Cursor」 |
| `research` | 要查资料、对比方案、深度调研 | 「调研」「对比」「优缺点」「为什么」「行业」 |
| `task-plan` | 列待办、步骤、今天要做的事 | 「待办」「清单」「先…再…」「步骤」 |
| `general-ai` | 以上都不明显；通用问答、翻译、写作 | 「帮我写」「解释一下」「什么意思」 |

**优先级**（Router 规则）：`code-editing` > `task-plan` > `research` > `general-ai`（仅当特征重叠时；具体见 `skills/router/SKILL.md`）。

## 与单 Skill 的对比

| 方案 | 优点 | 缺点 |
|------|------|------|
| 单 Skill 全包 | 一次 API | 容易偏代码或偏泛化；prompt 过长 |
| **Router + Specialist** ✅ | 场景准、prompt 短、易扩展 | 两次调用、略增延迟 |

延迟：PTT 短句场景通常可接受（Router 用 `gpt-4o-mini` 级，各 <1s 级，视 API 而定）。

## 上游来源（Specialist）

| intent | 主要 fork 自 |
|--------|----------------|
| code-editing | [code-editing.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/development/code-editing.md) |
| general-ai | [general-prompt.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/general-prompt.md) |
| research | [deep-research-prompt.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/deep-research-prompt.md) |
| task-plan | [to-do-list.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/to-do-list.md) |

共用清理逻辑：[Voice-Prompt-Enhancement-Node](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node)、[STT-Basic-Cleanup](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt) → `skills/shared/stt-base.md`。

## 可选增强（后续）

- **托盘强制意图**：用户已知在写代码时选「编程」，省 Router 调用。
- **项目 Glossary**：参考 [gh-aw dictation skill](https://github.com/majiayu000/claude-skill-registry/blob/main/skills/other/dictation-githubnext-gh-aw-2/SKILL.md)，为 STT 误听加项目专有词替换表。
- **本地关键词预路由**：明显含 `async`/`refactor` 等可跳过 Router（省 token），不确定再走 LLM。
