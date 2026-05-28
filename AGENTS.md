# Agent 指南

在本仓库执行开发、调试或打包任务前，**请先阅读：**

**[`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md)**

## 通用 Cursor 规则（强制）

本仓库已安装 [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule)。**所有 Agent 任务**须遵守 `.cursor/rules/` 中 `alwaysApply` 规则：

| 规则 | 作用 |
|------|------|
| [`00-universal-core.mdc`](.cursor/rules/00-universal-core.mdc) | 完成定义、本地/云端分工、文档优先于随意改 CI |
| [`exe-packaging-local-cloud.mdc`](.cursor/rules/exe-packaging-local-cloud.mdc) | 本地 `watch-build-release.ps1 -Once`；云端验证 Build release EXE |
| [`post-push-ci-green.mdc`](.cursor/rules/post-push-ci-green.mdc) | push 后盯 Actions 至绿（本地可 `.cursor/.local-skip-post-push-ci` 跳过） |
| [`docs-sync-before-finish.mdc`](.cursor/rules/docs-sync-before-finish.mdc) | 任务结束前同步全仓库 `.md` / `.txt` |
| [`git-track-cursor-folder.mdc`](.cursor/rules/git-track-cursor-folder.mdc) | `.cursor/` 必须进 git |

刷新通用规则：`.\scripts\sync-universal-cursor-rules.ps1 -Refresh`（版本锁定见 `.cursor/UNIVERSAL_RULE_LOCK`）

## 任务收尾自检（不可跳过）

规则不会替你执行命令。**每次**改代码并 `git push`（或向用户报「做完」）前，Agent 必须自己跑完 applicable 项；不得以「改动小」省略。

| 步骤 | 条件 | 动作 |
|------|------|------|
| 文档同步 | 任何实质性改动 | 按 `docs-sync-before-finish.mdc` 扫 `.md`/`.txt`；收尾列出改过哪些文档 |
| 打包 exe | 动了 `src/`、`ui/`、打包脚本 | 本地：`.\scripts\watch-build-release.ps1 -Once`；云端：确认 **Build release EXE** workflow 已绿 |
| push 后 CI | 已 `git push` 且未建 `.cursor/.local-skip-post-push-ci` | `gh run list` + `gh run watch --exit-status`；红则修→push→再看 |
| `.cursor/` 进库 | 改了 `.cursor/**` | `git add .cursor/` 并确认未被子目录 ignore |

**Cursor skill 调用名** 与文件夹一致、用连字符，例如 `/frontend-design`（不是 `/frontend design`）。Skill 的 `SKILL.md` 须有 `name` + `description` frontmatter。

## 本仓库专项

该文档包含：

- Windows / .NET 8 SDK 前置条件  
- 克隆 → 下载模型 → 编译 → 测试 → 运行的完整步骤  
- `models/` 迁移与 gitignore 说明  
- 唤醒词、日志路径、打包命令与常见问题  
- Agent 改代码后的验证清单  

产品架构与已定稿决策见 [`README.md`](README.md)。

**WebView2 统一 UI（路线 B）** 见 [`docs/UI_ROUTE_B_WEBVIEW2.md`](docs/UI_ROUTE_B_WEBVIEW2.md)。  
**Web UI 视觉（马卡龙色系）** 见 [`.cursor/skills/frontend-design/SKILL.md`](.cursor/skills/frontend-design/SKILL.md)；Agent 内手动调用：**`/frontend-design`**。  
**CI 排错** 见 [`.cursor/skills/github-actions-ci/SKILL.md`](.cursor/skills/github-actions-ci/SKILL.md)。  
**Cursor 目录说明** 见 [`.cursor/README.md`](.cursor/README.md)。
