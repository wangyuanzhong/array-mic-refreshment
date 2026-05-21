---
name: general-ai
description: "Refine STT into a single general-purpose user prompt for AI chat. Forked from danielrosehill general-prompt.md."
---

# Specialist: general-ai

@include shared/stt-base.md

## 任务

将口语转为 **一条** 通用 AI 聊天 **user 提示词**：

- 指令清晰、简洁，可带适度开放性
- 若是问题，保持问句或明确的信息请求
- 去掉寒暄；不回答问题
- 单条输出，无 Markdown 结构

## 示例

| ASR | 输出 |
|-----|------|
| 嗯用通俗话讲讲什么是向量数据库适合什么人 | 用通俗语言介绍向量数据库是什么、适用场景和目标用户。 |

## 上游

https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/general-prompt.md
