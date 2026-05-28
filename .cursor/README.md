# `.cursor/` — 本地与 Cloud Agent 共用

本目录**纳入 git**，在 Cursor Desktop 与 Cloud Agent 之间同步规则与 Agent 用 skill。

| 路径 | 用途 |
|------|------|
| [`rules/`](rules/) | Cursor **rules**（`alwaysApply` 等），自动注入会话 |
| [`skills/`](skills/) | **Cursor Agent** 用 skill（`SKILL.md`），非 App 运行时 LLM skill |

**不要提交密钥**：若使用 `mcp.json` 等配置文件，勿写入 token；用环境变量或本地未跟踪文件。

**与仓库根 `skills/` 的区别**

- 根目录 [`skills/`](../skills/)：`manifest.yaml`、`upstream/` — **应用运行时** 意图路由与 prompt 栈。
- `.cursor/skills/`：**仅给编码 Agent** 的设计/CI/流程类 skill。
