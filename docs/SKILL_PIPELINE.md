# 多 Skill 协同管线（仅第三方 prompt）

## 原则

- **不自写** Specialist / Router 正文。
- 正文 = [`skills/upstream/`](../skills/upstream/) 内复制的他人 prompt 或 SKILL。
- 本仓库只提供 [`skills/manifest.yaml`](../skills/manifest.yaml)（路径拼接 + intent 映射）。

## 流程

```text
ASR 原文
  → LLM #1 system = upstream/shanttoosh/voice-controlled-ai-agent.intent-classification.md
       （来自 shanttoosh/voice-controlled-ai-agent/agent/intent.py）
  → JSON intent → manifest.intent_map → specialist 名
  → LLM #2 system = 按 manifest 顺序拼接多个 upstream/*.md（prompt stack）
  → 剪贴板
```

## intent 映射（配置，非新 prompt）

| 上游 classifier 输出 | 本应用 specialist | 使用的他人文件栈 |
|---------------------|-------------------|------------------|
| `write_code` | `code-editing` | STT-Basic-Cleanup + Voice-Prompt-Enhancement-Node + code-editing.md |
| `general_chat` | `general-ai` | STT-Basic-Cleanup + general-prompt.md |
| `summarize` | `research` | STT-Basic-Cleanup + deep-research-prompt.md |
| `create_file` | `task-plan` | STT-Basic-Cleanup + to-do-list.md |

设置页 **强制意图** 时跳过 LLM #1，直接选 specialist 键名。

## 维护

```bash
./scripts/sync-upstream-skills.sh
```

更新 [`skills/manifest.yaml`](../skills/manifest.yaml) 仅当增删 specialist 或改映射，**不要**在 manifest 里写长 prompt。

## 可选 Skill

见 manifest `optional_skills`：`voice-refine`（FlorianBruniaux）、`dictation-githubnext-gh-aw-2`（majiayu000）。完整 SKILL.md 文件在 `upstream/`。
