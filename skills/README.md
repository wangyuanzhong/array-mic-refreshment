# Skills — 只用第三方原文，不自写正文（v0.1）

应用内置 **纯文本整理** 使用 `upstream/array-mic/plain-text-polish.md`（与代码同步）。使用 **Qwen3 / Qwen3.5** 时会在 system 与 user 末尾加 **`/no_think`** 关闭 thinking。其他整理风格使用下方 manifest 与 upstream 栈。

本目录 **不包含自研 Skill 正文**。流程由 [`manifest.yaml`](manifest.yaml) 配置，正文来自 [`upstream/`](upstream/) 下复制的第三方 prompt / SKILL。

## 流程

```text
ASR 原文
  → ① upstream/shanttoosh/...intent-classification.md   （shanttoosh/voice-controlled-ai-agent）
  → manifest.intent_map 映射到 specialist 名
  → ② 按 manifest 拼接 upstream/danielrosehill/... 的 prompt stack
  → 剪贴板
```

## 关键文件

| 文件 | 作用 |
|------|------|
| [`manifest.yaml`](manifest.yaml) | 路由用哪个上游文件、intent 映射、各场景 prompt **栈** |
| [`upstream/`](upstream/) | 第三方原文副本 |
| [`scripts/sync-upstream-skills.sh`](../scripts/sync-upstream-skills.sh) | 从 GitHub 拉取最新上游 |

## 默认 Specialist 栈（均为他人作品）

| specialist | 上游（danielrosehill 等） |
|------------|---------------------------|
| `code-editing` | STT-Basic-Cleanup + Voice-Prompt-Enhancement-Node + code-editing.md |
| `general-ai` | STT-Basic-Cleanup + general-prompt.md |
| `research` | STT-Basic-Cleanup + deep-research-prompt.md |
| `task-plan` | STT-Basic-Cleanup + to-do-list.md |

## 可选完整 Skill（设置页选用，非默认）

| 键 | 上游 |
|----|------|
| `voice_refine` | [FlorianBruniaux/voice-refine](https://github.com/FlorianBruniaux/claude-code-ultimate-guide/blob/main/examples/skills/voice-refine/SKILL.md) |
| `dictation_cleanup` | [majiayu000/dictation-githubnext-gh-aw-2](https://github.com/majiayu000/claude-skill-registry/blob/main/skills/other/dictation-githubnext-gh-aw-2/SKILL.md) |

## C# 实现要点

```csharp
// 读取 skills/manifest.yaml
// Router: File.ReadAllText(router.system_prompt_file)
// Map: upstream "write_code" -> specialist "code-editing"
// Refine: string.Join("\n\n", stack files)
```

## 已废弃路径

`skills/router/SKILL.md`、`skills/intents/*`、`skills/shared/` 为早期自写草案，**已删除**；勿再引用。
