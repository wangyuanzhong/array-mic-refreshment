# 多 Skill 协同管线（v0.1）

设置页 **纯文本整理**（`PromptIntent.PlainText`）使用精简 prompt：`skills/upstream/array-mic/plain-text-polish.md`（与代码内 `DefaultPolishPrompt` 同步）。为兼容 **Qwen3 / Qwen3.5** 本地推理，system 与 user 消息末尾均附加官方软开关 **`/no_think`**（禁用 thinking，避免占满 Max Tokens 导致 `content` 为空）。不加载 danielrosehill 长栈。

其他整理风格仍走下方「第三方 prompt 栈」。

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
| `write_code` | `code-editing` | minimal cleanup + `software-product-requirements` + fidelity（产品/流程需求，非 code-editing.md） |
| `general_chat` | `general-ai` | minimal + general-prompt + fidelity |
| `summarize` | `research` | minimal + deep-research-prompt + research-fidelity + fidelity |
| `create_file` | `task-plan` | minimal + to-do-list + fidelity |

设置页 **强制意图** 时跳过 LLM #1，直接选 specialist 键名（`forcedSpecialistKey`，与「整理风格管理」表格、功能预设下拉同源）。

用户可在 Skills 目录下 `refinement-styles/*.md` 增加自定义风格（YAML frontmatter：`name`、`description`、`id`、可选 `stack`；无 stack 时正文为 system prompt）。

## 维护

```bash
./scripts/sync-upstream-skills.sh
```

更新 [`skills/manifest.yaml`](../skills/manifest.yaml) 仅当增删 specialist 或改映射，**不要**在 manifest 里写长 prompt。

## 可选 Skill

见 manifest `optional_skills`：`voice-refine`（FlorianBruniaux）、`dictation-githubnext-gh-aw-2`（majiayu000）。完整 SKILL.md 文件在 `upstream/`。
