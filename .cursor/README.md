# `.cursor/` — 本地与 Cloud Agent 共用

规则包：[cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule) **0.9.0**（版本见 [`UNIVERSAL_RULE_LOCK`](UNIVERSAL_RULE_LOCK)）。

| 路径 | 作用 |
|------|------|
| [`rules/`](rules/) | 通用 `alwaysApply` 规则 + 本仓库追加段（`apply-amr-cursor-overlays.ps1`） |
| [`skills/frontend-design/`](skills/frontend-design/) | 可选 Agent skill：UI 视觉（`/frontend-design`） |

自 **0.9.0** 起，上游不再提供 `github-actions-ci` skill；CI 排错内容在 [`rules/post-push-ci-green.mdc`](rules/post-push-ci-green.mdc)（含通用 fix patterns 表与本仓库 workflow 段）。

刷新：

```powershell
.\scripts\sync-universal-cursor-rules.ps1 -Refresh
```

等价手工安装（见 universal README）：

```bash
git clone --depth 1 https://github.com/wangyuanzhong/cursor-universal-rule.git /tmp/cursor-universal-rule
mkdir -p .cursor/rules
cp /tmp/cursor-universal-rule/rules/*.mdc .cursor/rules/
.\scripts\apply-amr-cursor-overlays.ps1
```

**Push 后 CI（强制）：** 见 [`rules/post-push-ci-green.mdc`](rules/post-push-ci-green.mdc) 与 [`rules/00-universal-core.mdc`](rules/00-universal-core.mdc) Done check。无 opt-out。

**本地自动 push（可选）：** 仅 `MODE: Local` 且仓库根存在 `.cursor/.local-auto-push` 时生效（见 [`rules/local-auto-push-current-branch.mdc`](rules/local-auto-push-current-branch.mdc)）。

勿提交 `.cursor/` 内的密钥。

根目录 [`skills/`](../skills/) 为 **App 运行时** LLM manifest/upstream，不是 Cursor `/` skill。
