# `.cursor/` — 本地与 Cloud Agent 共用

规则包：[cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule)（版本见 [`UNIVERSAL_RULE_LOCK`](UNIVERSAL_RULE_LOCK)）。

| 路径 | 作用 |
|------|------|
| [`rules/`](rules/) | 通用 `alwaysApply` 规则 + 本仓库覆盖（`apply-amr-cursor-overlays.ps1`） |
| [`skills/`](skills/) | Agent skill：`frontend-design`、`github-actions-ci` |

刷新：`.\scripts\sync-universal-cursor-rules.ps1 -Refresh`

**Skill 调用：** `/frontend-design`、`/github-actions-ci`（连字符）。`SKILL.md` 须有 `name` + `description` frontmatter。

**Push 后 CI（强制）：** 见 [`rules/post-push-ci-green.mdc`](rules/post-push-ci-green.mdc)。`gh run watch` 直到当前分支触发的工作流全绿；EXE 仓库须含 **Windows** `build-windows`（App.Tests）与 **Build release EXE**。`.cursor/.local-skip-post-push-ci` **已废弃**，勿再使用。

勿提交 `.cursor/` 内的密钥。

根目录 [`skills/`](../skills/) 为 **App 运行时** LLM manifest/upstream，不是 Cursor `/` skill。
