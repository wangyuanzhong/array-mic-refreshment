# `.cursor/` — 本地与 Cloud Agent 共用

规则包：[cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule)（版本见 [`UNIVERSAL_RULE_LOCK`](UNIVERSAL_RULE_LOCK)）。

| 路径 | 作用 |
|------|------|
| [`rules/`](rules/) | 通用 `alwaysApply` 规则 + 本仓库覆盖（`apply-amr-cursor-overlays.ps1`） |
| [`skills/`](skills/) | Agent skill：`frontend-design`、`github-actions-ci` |

刷新：`.\scripts\sync-universal-cursor-rules.ps1 -Refresh`

**Skill 调用：** `/frontend-design`、`/github-actions-ci`（连字符）。`SKILL.md` 须有 `name` + `description` frontmatter。

**本地跳过 push 后盯 CI：** `.cursor/.local-skip-post-push-ci` 或 `.\scripts\cursor-local-opt-out-post-push-ci.ps1`

勿提交 `.cursor/` 内的密钥。

根目录 [`skills/`](../skills/) 为 **App 运行时** LLM manifest/upstream，不是 Cursor `/` skill。
