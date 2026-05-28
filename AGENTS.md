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

刷新通用规则：`.\scripts\sync-universal-cursor-rules.ps1`

## 本仓库专项

该文档包含：

- Windows / .NET 8 SDK 前置条件  
- 克隆 → 下载模型 → 编译 → 测试 → 运行的完整步骤  
- `models/` 迁移与 gitignore 说明  
- 唤醒词、日志路径、打包命令与常见问题  
- Agent 改代码后的验证清单  

产品架构与已定稿决策见 [`README.md`](README.md)。

**WebView2 统一 UI（路线 B）** 见 [`docs/UI_ROUTE_B_WEBVIEW2.md`](docs/UI_ROUTE_B_WEBVIEW2.md)。  
**Web UI 视觉（马卡龙色系）** 见 [`.cursor/skills/frontend-design/SKILL.md`](.cursor/skills/frontend-design/SKILL.md)。  
**CI 排错** 见 [`.cursor/skills/github-actions-ci/SKILL.md`](.cursor/skills/github-actions-ci/SKILL.md)。  
**Cursor 目录说明** 见 [`.cursor/README.md`](.cursor/README.md)。
