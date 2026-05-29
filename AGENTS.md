# Agent 指南

在本仓库执行开发、调试或打包任务前，**请先阅读：**

**[`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md)**

## 通用 Cursor 规则（强制）

本仓库已安装 [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule) **0.9.1**（见 [`.cursor/UNIVERSAL_RULE_LOCK`](.cursor/UNIVERSAL_RULE_LOCK)）。**所有 Agent 任务**须遵守 `.cursor/rules/` 中 `alwaysApply` 规则。权威收尾见 **[`00-universal-core.mdc`](.cursor/rules/00-universal-core.mdc)**：计划里写 **`MODE:`** 与 **`Closing: I will end this reply with the verbatim Done check.`**；每轮改文件的回复**末尾主动**输出 verbatim **Done check**（勿等用户追问）。

| 规则 | 作用 |
|------|------|
| [`00-universal-core.mdc`](.cursor/rules/00-universal-core.mdc) | **最重要**：主动 Done check；计划两行 MODE + Closing；每个 agent 完整 hygiene；子 Agent + parent verification |
| [`post-push-ci-green.mdc`](.cursor/rules/post-push-ci-green.mdc) | push 后 `gh run watch` 至绿（含 **Common CI fix patterns**；**无跳过**；须 **Windows** `build-windows`） |
| [`docs-sync-before-finish.mdc`](.cursor/rules/docs-sync-before-finish.mdc) | 逐文件 `Docs review:` + **change-impact grep sweep**（禁止聚合偷懒） |
| [`versioning-and-changelog.mdc`](.cursor/rules/versioning-and-changelog.mdc) | 每次 push 写 CHANGELOG + 版本号 |
| [`exe-packaging-local-cloud.mdc`](.cursor/rules/exe-packaging-local-cloud.mdc) | 本地 `watch-build-release.ps1 -Once`；云端验证 **Build release EXE** |
| [`local-auto-push-current-branch.mdc`](.cursor/rules/local-auto-push-current-branch.mdc) | **仅 Local** 且存在 `.cursor/.local-auto-push` 时自动 commit/push |
| [`git-track-cursor-folder.mdc`](.cursor/rules/git-track-cursor-folder.mdc) | `.cursor/` 必须进 git |

刷新通用规则：

```powershell
.\scripts\sync-universal-cursor-rules.ps1 -Refresh
```

版本锁定见 [`.cursor/UNIVERSAL_RULE_LOCK`](.cursor/UNIVERSAL_RULE_LOCK)。同步后会自动执行 `apply-amr-cursor-overlays.ps1`（**追加**本仓库专项，不覆盖通用正文）。

## Cloud Agent 必读（MODE: Cloud）

1. 任务开始计划在写 `MODE: Cloud` 的同时写 `Closing: I will end this reply with the verbatim Done check.`（见 `00-universal-core.mdc`）。
2. **不能**在云端跑 WinForms exe / `watch-build-release.ps1`；用 CI 验证（`build-windows` + `build-release-exe`）。
3. 使用 **Task 子 Agent** 时：默认 Scenario B（子 Agent **不得** push）；若子 Agent 自己 push，父 Agent 必须按 `00-universal-core` 验证其 Done check、CHANGELOG、CI。
4. 收尾必须输出完整 **Done check** + `Docs review:` 逐文件列表 + CI run ID（若 push 过）。

## 本仓库专项文档

| 文档 | 内容 |
|------|------|
| [`README.md`](README.md) | 产品架构与已定稿决策 |
| [`docs/UI_ROUTE_B_WEBVIEW2.md`](docs/UI_ROUTE_B_WEBVIEW2.md) | WebView2 路线 B、Bridge API、Phase 验收 |
| [`CHANGELOG.md`](CHANGELOG.md) | 版本变更 |

**自动化（Windows）：** `.\scripts\test-phase2-route-b.ps1`、`.\scripts\test-feature-presets.ps1`

**Web HUD（Phase 4）：** 默认 `UseWebStatusHud`；设置页可关，或 `AMR_WEB_HUD=0` 强制原生条。修改后需重启应用。

**Skill 调用（可选）：** `/frontend-design` — 见 [`.cursor/README.md`](.cursor/README.md)。CI 排错见 [`post-push-ci-green.mdc`](.cursor/rules/post-push-ci-green.mdc)（不再使用 `github-actions-ci` skill）。
