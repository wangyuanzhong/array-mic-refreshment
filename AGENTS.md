# Agent 指南

在本仓库执行开发、调试或打包任务前，**请先阅读：**

**[`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md)**

该文档包含：

- Windows / .NET 8 SDK 前置条件  
- 克隆 → 下载模型 → 编译 → 测试 → 运行的完整步骤  
- `models/` 迁移与 gitignore 说明  
- 唤醒词、日志路径、打包命令与常见问题  
- Agent 改代码后的验证清单  

产品架构与已定稿决策见 [`README.md`](README.md)。

**改 App / Web UI 后请打 exe：** `.\scripts\watch-build-release.ps1 -Once`（或让用户常驻 `.\scripts\watch-build-release.ps1` 监视保存）。详见 [`.cursor/rules/auto-build-exe.mdc`](.cursor/rules/auto-build-exe.mdc)。

**WebView2 统一 UI（路线 B）** 的实施说明、Bridge 契约与分阶段 checklist 见 [`docs/UI_ROUTE_B_WEBVIEW2.md`](docs/UI_ROUTE_B_WEBVIEW2.md)。  
**Web UI 视觉（马卡龙色系）** 见 [`skills/frontend-design/SKILL.md`](skills/frontend-design/SKILL.md)。
