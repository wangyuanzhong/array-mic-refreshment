# Skill 调研：STT 原文 → 代码编辑指令

> 调研日期：2026-05。产品默认场景：**对着 IDE 说修改需求 → 粘贴进 AI 编程助手**。

---

## 已定稿

| 项 | 选择 |
|----|------|
| 默认 Skill | [`skills/prompt-refine/SKILL.md`](../skills/prompt-refine/SKILL.md) |
| 侧重点 | **代码编辑指令**（非通用聊天、非多段 Markdown 任务书） |
| 主上游 | `danielrosehill/Text-Transformation-Prompt-Collection-2` → **`code-editing.md`** |

---

## 与本产品最相关的上游

| 仓库 | 文件 | 匹配度 | 说明 |
|------|------|--------|------|
| [Text-Transformation-Prompt-Collection-2](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2) | [`by-use-case/ai/development/code-editing.md`](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/development/code-editing.md) | ⭐⭐⭐⭐⭐ | **口语 → AI 代码编辑工具指令**；实现向、具体变更 |
| [Voice-Prompt-Enhancement-Node](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node) | [`prompt.md`](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node/blob/main/prompt.md) | ⭐⭐⭐⭐⭐ | STT→推理 prompt；去 filler、改口、无包装输出 |
| [STT-Basic-Cleanup-System-Prompt](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt) | [`complete-system-prompt.md`](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt/blob/main/complete-system-prompt.md) | ⭐⭐⭐⭐ | STT 缺陷修复、保留原意 |
| [Speech-Tech-Index](https://github.com/danielrosehill/Speech-Tech-Index) | README | ⭐⭐⭐⭐ | Transcript Processing / Voice Automation 索引 |
| [claude-code-ultimate-guide](https://github.com/FlorianBruniaux/claude-code-ultimate-guide) | [`voice-refine/SKILL.md`](https://github.com/FlorianBruniaux/claude-code-ultimate-guide/blob/main/examples/skills/voice-refine/SKILL.md) | ⭐⭐⭐ | Skill 格式；输出为多段 Markdown，**未作默认** |

### 曾考虑但未作默认

| 文件 | 原因 |
|------|------|
| `general-prompt.md` | 太泛，不适合「改代码」主场景 |
| `voice-refine` 四段模板 | 适合 Claude Code 长任务，不适合单条粘贴进聊天框 |

---

## 其他可参考

| 仓库 | 用途 |
|------|------|
| [dictation-githubnext-gh-aw-2/SKILL.md](https://github.com/majiayu000/claude-skill-registry/blob/main/skills/other/dictation-githubnext-gh-aw-2/SKILL.md) | 英文/GitHub 术语 STT 纠错表，可按项目加 glossary |
| [Voice-To-Prompt-Pipeline](https://github.com/danielrosehill/Voice-To-Prompt-Pipeline) | 多段 Summary/Requests（我们是文本 API + 单条） |
| [Speech-To-Text-System-Prompt-Library](https://github.com/danielrosehill/Speech-To-Text-System-Prompt-Library) | Prompt 栈；未来可做「邮件 / 文档」第二 Skill |

---

## 合成方式

见 [`skills/prompt-refine/ATTRIBUTION.md`](../skills/prompt-refine/ATTRIBUTION.md)：

1. **输出形态** ← `code-editing.md`
2. **STT 清理与改口** ← Voice-Prompt-Enhancement-Node + STT-Basic-Cleanup
3. **本产品约束** ← 单条、无 Markdown 结构、针对 Cursor/Copilot 聊天框
