# Skill 调研：STT 原文 → AI 提示词整理

> 调研日期：2026-05。目标：为「按住说话 → 本地 ASR → LLM 整理成可发给聊天机器人的一句提示词」找**已有**、可复用的 Skill / System Prompt，而非从零编造。

---

## 与本产品最相关的上游（推荐优先阅读）

| 仓库 | 文件 | 匹配度 | 说明 |
|------|------|--------|------|
| [danielrosehill/Voice-Prompt-Enhancement-Node](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node) | [`prompt.md`](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node/blob/main/prompt.md) | ⭐⭐⭐⭐⭐ | **专门**把 STT 原文优化成「给推理 Agent 用的 prompt」；去 filler、纠听写错、分段、prompt engineering |
| [danielrosehill/STT-Basic-Cleanup-System-Prompt](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt) | [`complete-system-prompt.md`](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt/blob/main/complete-system-prompt.md) | ⭐⭐⭐⭐ | 明确写「文本来自 STT」；标点、段落、保留用户声音、**无前后缀输出** |
| [danielrosehill/Speech-Tech-Index](https://github.com/danielrosehill/Speech-Tech-Index) | README 索引 | ⭐⭐⭐⭐ | **Speech → 清理 → 变换** 全链路索引；`Transcript Processing` / `Voice Automation` 章节 |
| [danielrosehill/Text-Transformation-Prompt-Collection-2](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2) | [`by-use-case/ai/general-prompt.md`](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/general-prompt.md) 等 | ⭐⭐⭐ | 大量「STT → 某格式」短 prompt；可叠在 basic cleanup 之上 |
| [FlorianBruniaux/claude-code-ultimate-guide](https://github.com/FlorianBruniaux/claude-code-ultimate-guide) | [`examples/skills/voice-refine/SKILL.md`](https://github.com/FlorianBruniaux/claude-code-ultimate-guide/blob/main/examples/skills/voice-refine/SKILL.md) | ⭐⭐⭐ | Claude **Skill 格式**（YAML frontmatter）；dedupe→extract→structure→compress；输出 **Markdown 多段**（Contexte/Objectif…），偏 Claude Code，需改成本产品「单条 user prompt」 |

### 本产品与 `voice-refine` 的差异

- `voice-refine`：长语音、压到 ~30% token、输出 **结构化 Markdown**（适合 Claude Code 任务）。
- **本产品**：PTT **短句**、剪贴板 **一条** 给任意 AI 聊天框 → 更需要 **Voice-Prompt-Enhancement-Node** + **general-prompt** 路线，而不是法语四段模板。

---

## 其他可参考

| 仓库 | 用途 |
|------|------|
| [majiayu000/claude-skill-registry/.../dictation-githubnext-gh-aw-2/SKILL.md](https://github.com/majiayu000/claude-skill-registry/blob/main/skills/other/dictation-githubnext-gh-aw-2/SKILL.md) | GitHub gh-aw **听写纠错** Skill：去 filler、专业词汇表、STT 误听替换（偏英文/GitHub 术语） |
| [danielrosehill/Voice-To-Prompt-Pipeline](https://github.com/danielrosehill/Voice-To-Prompt-Pipeline) | 多模态 Gemini 音频→Summary/Requests/Context；思路可参考，但我们是 **文本 API** 而非音频 multimodal |
| [danielrosehill/Voice-Cleanup-Prompt-Experiment](https://github.com/danielrosehill/Voice-Cleanup-Prompt-Experiment) | 对比多种 cleanup system prompt 效果 |
| [danielrosehill/Speech-To-Text-System-Prompt-Library](https://github.com/danielrosehill/Speech-To-Text-System-Prompt-Library) | **Prompt 栈**（basic cleanup + format + tone）；适合「可组合」设置页进阶模式 |
| [human37/open-wispr](https://github.com/human37/open-wispr) / [yatharthsameer/Wispr-Flow](https://github.com/yatharthsameer/Wispr-Flow) | 商业 Wispr 类：本地 STT + 可选 API 格式化；产品形态接近，prompt 多在应用内未单独开源 |

---

## 本仓库采用的策略

`skills/prompt-refine/SKILL.md` **不是凭空编写**，而是：

1. **主骨架**：`Voice-Prompt-Enhancement-Node/prompt.md`（STT→LLM prompt 优化）
2. **清理规则**：`STT-Basic-Cleanup-System-Prompt`（STT 缺陷、无前后缀、保留原意）
3. **输出形态**：`Text-Transformation-Prompt-Collection-2/by-use-case/ai/general-prompt.md`（单条 general-purpose AI prompt）
4. **Skill 封装格式**：参考 `voice-refine` 的 YAML frontmatter（Cursor/Claude Skills 习惯）
5. **本产品约束**：单条输出、禁止 Markdown 小节（适配微信/Cursor 输入框粘贴）

详见 `skills/prompt-refine/ATTRIBUTION.md`。
