# `.cursor/` — 本地与 Cloud Agent 共用

本目录**纳入 git**，在 Cursor Desktop 与 Cloud Agent 之间同步规则与 Agent 用 skill。

## 规则来源

| 来源 | 路径 |
|------|------|
| **[cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule)** | `.cursor/rules/00-universal-core.mdc`, `exe-packaging-local-cloud.mdc`, `post-push-ci-green.mdc`, `docs-sync-before-finish.mdc`, `git-track-cursor-folder.mdc` |
| **本项目** | `.cursor/skills/frontend-design/`，以及 `github-actions-ci/SKILL.md` 中的 AMR 排错补充 |

刷新通用规则（保留后需检查 AMR 覆盖段）：

```powershell
.\scripts\sync-universal-cursor-rules.ps1
```

## 目录

| 路径 | 用途 |
|------|------|
| [`rules/`](rules/) | Cursor **rules**（`alwaysApply`），自动注入每次会话 |
| [`skills/`](skills/) | **Cursor Agent** 用 skill（非 App 运行时 `skills/manifest.yaml`） |

## 本地 opt-out

不想在 **Desktop** 上 push 后盯 CI：创建空文件

```text
.cursor/.local-skip-post-push-ci
```

或运行 `.\scripts\cursor-local-opt-out-post-push-ci.ps1`。

Cloud Agent **不会**因该文件跳过 CI 规则。

## 安全

勿在 `.cursor/` 提交 token（如 `mcp.json` 中的密钥）。

## 与仓库根 `skills/` 的区别

- 根目录 [`skills/`](../skills/)：`manifest.yaml`、`upstream/` — **应用运行时** LLM 路由与 prompt 栈。
- `.cursor/skills/`：**仅给编码 Agent** 的设计 / CI / 流程说明。
