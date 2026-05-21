# Skill 调研：只用他人成品

> 策略：**复制上游 prompt / SKILL 原文** + `manifest.yaml` 做路由与拼接。不在本仓库发明 Skill 正文。

---

## 实际采用的第三方资源

| 角色 | 仓库 | 本仓库路径 |
|------|------|------------|
| 意图分类 | [shanttoosh/voice-controlled-ai-agent](https://github.com/shanttoosh/voice-controlled-ai-agent) `agent/intent.py` | `upstream/shanttoosh/voice-controlled-ai-agent.intent-classification.md` |
| STT 清理底层 | [danielrosehill/STT-Basic-Cleanup-System-Prompt](https://github.com/danielrosehill/STT-Basic-Cleanup-System-Prompt) | `upstream/danielrosehill/stt-basic-cleanup.complete-system-prompt.md` |
| 语音→LLM prompt | [danielrosehill/Voice-Prompt-Enhancement-Node](https://github.com/danielrosehill/Voice-Prompt-Enhancement-Node) | `upstream/danielrosehill/voice-prompt-enhancement-node.prompt.md` |
| 编程指令 | [Text-Transformation-Prompt-Collection-2/code-editing.md](https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/development/code-editing.md) | `upstream/danielrosehill/text-transform/code-editing.md` |
| 通用 AI prompt | …/general-prompt.md | `…/general-prompt.md` |
| 深度调研 | …/deep-research-prompt.md | `…/deep-research-prompt.md` |
| 待办清单 | …/to-do-list.md | `…/to-do-list.md` |
| 可选 Claude Skill | [FlorianBruniaux/…/voice-refine/SKILL.md](https://github.com/FlorianBruniaux/claude-code-ultimate-guide/blob/main/examples/skills/voice-refine/SKILL.md) | `upstream/florianbruniaux/voice-refine.SKILL.md` |
| 可选听写纠错 Skill | [majiayu000/…/dictation-githubnext-gh-aw-2/SKILL.md](https://github.com/majiayu000/claude-skill-registry/blob/main/skills/other/dictation-githubnext-gh-aw-2/SKILL.md) | `upstream/majiayu000/dictation-githubnext-gh-aw-2.SKILL.md` |

索引库：[danielrosehill/Speech-Tech-Index](https://github.com/danielrosehill/Speech-Tech-Index)（Transcript Processing 章节）。

---

## 为何不用自写 `skills/intents/*.md`

早期草案已删除。自写内容易与上游漂移；改为 **sync 脚本 + manifest** 更易核对出处与升级。

---

## 相关文档

- [`skills/README.md`](../skills/README.md)
- [`skills/manifest.yaml`](../skills/manifest.yaml)
- [`docs/SKILL_PIPELINE.md`](SKILL_PIPELINE.md)
