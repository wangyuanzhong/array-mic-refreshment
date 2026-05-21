---
name: code-editing
description: "Refine STT into a single coding instruction for AI IDE chat. Forked from danielrosehill code-editing.md."
---

# Specialist: code-editing

@include shared/stt-base.md（实现时由程序拼接 `skills/shared/stt-base.md`）

## 任务

将口语转为 **一条** 面向 AI 编程助手（Cursor/Copilot/Claude）的 **代码修改 / 实现指令**。

- 精确、实现向：rename、refactor、add handler、change signature…
- 落实为具体代码层面变更，避免空泛「优化一下」
- 保留路径、标识符、框架名、API 名
- 不写代码、不执行、无 Markdown 标题/代码围栏

## 示例

| ASR | 输出 |
|-----|------|
| 把这个 service 依赖注入改一下现在写死在构造函数 | 重构该 service：将构造函数内硬编码依赖改为依赖注入。 |

## 上游

https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/development/code-editing.md
