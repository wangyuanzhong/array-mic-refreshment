# 本地开发环境搭建指南

本文档面向 **在新 Windows 机器上克隆仓库后快速跑起来** 的开发者，以及 **Cursor / Agent** 自动执行环境搭建。按顺序完成「快速检查清单」即可开始开发与实机调试。

> **仓库：** https://github.com/wangyuanzhong/array-mic-refreshment  
> **当前产品版本：** 见根目录 [`VERSION.txt`](../VERSION.txt)（README 可能写 V0.3，以代码与 CHANGELOG 为准）

---

## 1. 环境要求（必读）

| 项目 | 要求 | 说明 |
|------|------|------|
| 操作系统 | **Windows 10/11 x64** | WinForms 托盘、`net8.0-windows`、NAudio、全局 PTT 热键、麦克风采集 **必须在 Windows 实机** |
| .NET | **.NET 8 SDK**（8.0.x） | 开发需 SDK，不是只装 Desktop Runtime |
| Git | 任意近期版本 | 克隆与提交 |
| 磁盘 | 建议 ≥ 8 GB 可用 | `models/` 约 1–3 GB；完整离线包约 2.7 GB |
| 网络 | 首次需能访问 GitHub | 模型由 `download-models.ps1` 从 GitHub Releases 下载 |
| 麦克风 | 任意录音设备 | 阵列麦 / USB 麦 / 内置麦均可；WASAPI 默认 |
| Python（可选） | 3.8+ | **仅**自定义唤醒词编码时需要；见 [§6 唤醒词](#6-唤醒词-kws-可选) |

**Linux / macOS：** 只能构建并测试 **跨平台类库**（`ArrayMicRefreshment.CI.slnf`），**不能**运行托盘 App 或做麦克风实机验证。CI 与此相同，见 [§8 测试](#8-测试)。

---

## 2. 快速检查清单（Agent 可直接逐步执行）

在新机器 PowerShell 中，于任意工作目录依次执行：

```powershell
# 0) 确认工具
dotnet --version          # 期望 8.x
git --version

# 1) 克隆
git clone https://github.com/wangyuanzhong/array-mic-refreshment.git
cd array-mic-refreshment

# 2) 下载模型（ASR + 声纹，默认）
.\scripts\download-models.ps1 -Package all

# 3) 若要开发/调试唤醒词，额外下载 KWS 模型
.\scripts\download-models.ps1 -IncludeKws

# 4) 若开发 Web 设置 UI（路线 B），先构建前端
cd ui
npm ci
npm run build
cd ..

# 5) 编译
dotnet build ArrayMicRefreshment.sln -c Release

# 6) 单元测试（不依赖麦克风）
dotnet test ArrayMicRefreshment.sln -c Release --filter "FullyQualifiedName!~Integration"

# 7) 运行托盘 App（需 Windows + 麦克风 + WebView2 Runtime）
dotnet run --project src\ArrayMicRefreshment.App -c Release
# 可选：AMR_USE_WINFORMS_SETTINGS=1 强制经典 WinForms 设置窗
```

**成功标志：**

- `dotnet build` 0 错误
- 托盘出现图标；右键可打开设置
- 按住 PTT（默认 `Ctrl+Alt+Space`）说话、松开后有识别结果
- 日志文件路径在启动时写入 `%AppData%\ArrayMicRefreshment\logs\`（见 [§7](#7-运行时路径与配置)）

---

## 3. 安装 .NET 8 SDK

若 `dotnet` 不存在或版本不是 8.x：

```powershell
winget install Microsoft.DotNet.SDK.8
```

安装后 **新开终端**，再执行 `dotnet --version`。

仅运行已打包的 `framework-dep` 版 exe 才需要 Desktop Runtime；**开发一律装 SDK**。

---

## 4. 克隆仓库与目录说明

```powershell
git clone https://github.com/wangyuanzhong/array-mic-refreshment.git
cd array-mic-refreshment
```

### 4.1 解决方案文件

| 文件 | 用途 |
|------|------|
| [`ArrayMicRefreshment.sln`](../ArrayMicRefreshment.sln) | **Windows 全量**：App + 所有库 + 全部测试（含 Integration） |
| [`ArrayMicRefreshment.CI.slnf`](../ArrayMicRefreshment.CI.slnf) | **Linux CI 子集**：不含 WinForms App |

### 4.2 关键目录

```text
array-mic-refreshment/
├── src/
│   ├── ArrayMicRefreshment.App/       # WinForms 托盘、设置窗、PTT 热键
│   ├── ArrayMicRefreshment.Core/      # 管道、设置、VoicePipeline
│   ├── ArrayMicRefreshment.Audio/     # 采集、PTT、WakeWordCapture
│   ├── ArrayMicRefreshment.Asr/       # SenseVoice ASR、Sherpa KWS
│   ├── ArrayMicRefreshment.Speaker/   # 声纹 enrollment / gate
│   ├── ArrayMicRefreshment.Prompt/    # LLM Router + Skill 整理
│   └── ArrayMicRefreshment.Output/    # 剪贴板、粘贴到光标
├── tests/                             # 单元测试 + Integration（Windows）
├── skills/                            # LLM Skill 映射（已在 git 中）
├── scripts/
│   ├── download-models.ps1            # 下载 models/（必跑）
│   ├── build-release.ps1              # 打 Release exe
│   ├── watch-build-release.ps1        # 监视改动并自动打 exe
│   ├── install-git-hooks.ps1          # 可选：commit 后自动打 exe
│   ├── generate-wake-encodings.ps1      # 唤醒词 ppinyin 编码（需 Python）
│   └── ModelManifest.json             # 模型 URL 清单
├── models/                            # ⚠ gitignore，克隆后不存在，必须下载或拷贝
├── dist/                              # ⚠ gitignore，本地打包输出
└── docs/                              # 设计与运维文档
```

### 4.3 不会进 git 的内容（迁移时注意）

| 路径 | 说明 |
|------|------|
| `models/` | 大模型，必须 `download-models.ps1` 或从旧机器拷贝 |
| `dist/`、`bin/`、`obj/` | 构建产物，不必拷贝 |
| `%AppData%\ArrayMicRefreshment\` | 用户设置、声纹 enrollment、日志 |
| `.cursor/` | **进 git** — 与 Cloud 共用的 rules / Agent skills（见 [`.cursor/README.md`](../.cursor/README.md)） |
| `agent-transcripts/` | 本地 Agent 对话缓存（不进 git） |

---

## 5. 下载模型（必做）

模型 **不在仓库内**。未下载时 App 会回退 stub，ASR / 声纹 / 唤醒词均不可用或行为异常。

在 **仓库根目录** 执行：

```powershell
# 最小：主 ASR + 声纹（默认已含声纹）
.\scripts\download-models.ps1

# 推荐开发：全部 ASR 包 + 声纹
.\scripts\download-models.ps1 -Package all

# 唤醒词开发：在上面的基础上加 KWS
.\scripts\download-models.ps1 -IncludeKws
```

### 5.1 模型清单（[`scripts/ModelManifest.json`](../scripts/ModelManifest.json)）

| 角色 | 目录名 | 说明 |
|------|--------|------|
| ASR 主模型 | `models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/` | int8，默认优先 |
| ASR 回退 | `models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/` | int8，带标点 |
| ASR 高精度 | `models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/` | float32，大且慢 |
| 声纹 | `models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k/*.onnx` | 说话人门禁 |
| 唤醒 KWS | `models/sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01/` | 需 `-IncludeKws` |

下载脚本会跳过已存在的目录；可重复执行。

### 5.2 从旧开发机迁移模型（省流量）

直接复制整个 `models\` 文件夹到新机器仓库根目录即可，无需重新下载。

### 5.3 验证模型是否就绪

```powershell
Test-Path models\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09
Test-Path models\3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k\*.onnx
# 唤醒词：
Test-Path models\sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01\tokens.txt
```

---

## 6. 唤醒词（KWS，可选）

唤醒词依赖 KWS 模型 + 中文短语 **ppinyin 编码**。

### 6.1 下载 KWS

```powershell
.\scripts\download-models.ps1 -IncludeKws
```

### 6.2 生成唤醒词编码（推荐，避免 `#0.30` 高阈值缓存）

需 Python 与 CLI：

```powershell
pip install sherpa-onnx click sentencepiece pypinyin
.\scripts\generate-wake-encodings.ps1
```

- 短语列表：[`scripts/wake-phrases.txt`](../scripts/wake-phrases.txt)
- 输出：`models/sherpa-onnx-kws-.../wake-phrase-encodings.json`

运行时 App 也会尝试用当前灵敏度 **动态生成** keywords 行；无 Python 时可能退回缓存行，唤醒率较差。日志中搜 `[WAKE-DIAG] wake keyword line` 确认是 `(generated)` 且 `threshold=0.050` 一类低阈值。

### 6.3 唤醒相关代码入口（Agent 改 bug 时）

| 文件 | 职责 |
|------|------|
| `src/ArrayMicRefreshment.Asr/SherpaKeywordWakeWordDetector.cs` | Sherpa KWS、AGC、诊断 |
| `src/ArrayMicRefreshment.Audio/WakeWordCaptureService.cs` | 监听 → 唤醒后会话 → 送 ASR |
| `src/ArrayMicRefreshment.Core/VoicePipeline.cs` | 唤醒/PTT 统一管道、声纹门禁 |
| `docs/WAKE_WORD_RUNTIME.md` | 架构说明（部分段落可能滞后，以代码为准） |

---

## 7. 运行时路径与配置

### 7.1 用户数据（本机持久化，不进 git）

| 路径 | 内容 |
|------|------|
| `%AppData%\ArrayMicRefreshment\settings.json` | 主设置：PTT 热键、触发模式、唤醒词、API、模型目录等 |
| `%AppData%\ArrayMicRefreshment\logs\app-YYYYMMDD.log` | Serilog 日志；唤醒诊断前缀 `[WAKE-DIAG]` |
| `%AppData%\ArrayMicRefreshment\speakers\` | 用户声纹 enrollment 数据 |

PowerShell 打开日志目录：

```powershell
explorer "$env:APPDATA\ArrayMicRefreshment\logs"
```

### 7.2 开发模式下的 `models/` 解析

默认 `AppSettings.ModelsDirectory = "models"`，相对 **当前工作目录** 或 **exe 所在目录** 解析（见 `ModelsPathResolver`）。

- `dotnet run --project src\ArrayMicRefreshment.App` 时，工作目录应为 **仓库根**，这样 `models\` 才能找到。
- 打包 exe 时，需把 `models\` 放在 exe 同级。

### 7.3 常用设置字段（[`AppSettings.cs`](../src/ArrayMicRefreshment.Core/AppSettings.cs)）

| 字段 | 默认 | 说明 |
|------|------|------|
| `PttHotkey` | `Ctrl+Alt+Space` | 全局 PTT |
| `TriggerMode` | `PttOnly` | 还可 `WakeWordOnly`、`Both` |
| `WakeWordPhrase` | `小助手` | 唤醒词文本 |
| `WakeWordSensitivity` | `Maximum` | KWS/AGC 灵敏度档位 |
| `SpeakerVerifyThreshold` | `0.40` | 声纹相似度阈值 |
| `ModelsDirectory` | `models` | 模型根目录 |
| `SkillsDirectory` | `skills` | LLM Skill |

---

## 8. 编译与运行

### 8.1 日常开发

```powershell
# 仓库根目录
dotnet build ArrayMicRefreshment.sln -c Release
dotnet run --project src\ArrayMicRefreshment.App -c Release
```

Debug 配置亦可；Release 更接近打包行为。

### 8.2 Visual Studio / Rider

打开 `ArrayMicRefreshment.sln`，启动项目设为 `ArrayMicRefreshment.App`，平台 **x64**，OS **Windows**。

---

## 9. 测试

### 9.1 单元测试（默认，无需模型、无需麦克风）

```powershell
dotnet test ArrayMicRefreshment.sln -c Release
```

排除 Integration（若不想跑 Windows 集成测）：

```powershell
dotnet test ArrayMicRefreshment.sln -c Release --filter "FullyQualifiedName!~Integration"
```

主要测试项目：

| 项目 | 覆盖 |
|------|------|
| `ArrayMicRefreshment.Core.Tests` | VoicePipeline、设置、ASR 文本抽取 |
| `ArrayMicRefreshment.Audio.Tests` | PTT、resample、唤醒 handoff |
| `ArrayMicRefreshment.Prompt.Tests` | Skill / Router |

### 9.2 集成测试（可选，Windows + 真实模型）

```powershell
$env:INTEGRATION_REAL_MODELS = "1"
dotnet test tests\ArrayMicRefreshment.Integration.Tests -c Release
```

需已下载 SenseVoice 到 `models/`。

### 9.3 Linux CI 等价命令

```bash
dotnet restore ArrayMicRefreshment.CI.slnf
./scripts/build-libraries.sh
dotnet test tests/ArrayMicRefreshment.Core.Tests -c Release
dotnet test tests/ArrayMicRefreshment.Audio.Tests -c Release
dotnet test tests/ArrayMicRefreshment.Prompt.Tests -c Release
```

---

## 10. 打包发布 exe

### 10.0 本地改代码后自动打包（可选）

| 方式 | 命令 | 何时触发 |
|------|------|----------|
| **保存即打包（推荐）** | `.\scripts\watch-build-release.ps1` | 监视 `src/`、`ui/`、`scripts/`；停笔约 12s 后自动 `build-release` |
| 单次打包 | `.\scripts\watch-build-release.ps1 -Once` | Agent / 手动 |
| **每次 git commit** | `.\scripts\install-git-hooks.ps1` | 安装 `.githooks/post-commit` |
| **推送到 GitHub** | Actions → `Build release EXE` | `main` 上改 App/UI 时上传 artifact |

监视日志：`dist\watch-build.log`。默认 **不** 复制 `models/`（快）；完整离线包仍用 `-IncludeModels` 或 `pack-ready.ps1`。

Cursor 共用配置：`.cursor/` — 通用规则来自 [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule)（`00-universal-core`、`exe-packaging-local-cloud`、`post-push-ci-green`、`docs-sync-before-finish`、`git-track-cursor-folder`）；刷新：`.\scripts\sync-universal-cursor-rules.ps1`。

Agent **skill**（非运行时 `skills/manifest.yaml`）：在 Agent 输入 **`/frontend-design`**（连字符）或 `@frontend-design`；`SKILL.md` 需含 YAML `name`/`description` 才会出现在 `/` 列表。详见 `.cursor/skills/README.md`。

**本机不想启用 push 后等 CI 全绿的 rule**（Cloud 仍保留）：不要写进全局 User Rules，在仓库根执行一次：

```powershell
.\scripts\cursor-local-opt-out-post-push-ci.ps1
```

恢复：`.\scripts\cursor-local-opt-out-post-push-ci.ps1 -Restore`

### 10.1 标准 self-contained 包

```powershell
# 先关闭正在运行的 App，避免 dist 目录被锁
Stop-Process -Name ArrayMicRefreshment -Force -ErrorAction SilentlyContinue

.\scripts\build-release.ps1 -Mode self-contained
# 输出：dist\ArrayMicRefreshment-self-contained\ArrayMicRefreshment.exe
```

把 `models\` 复制到 exe 同级（若未使用 `-IncludeModels`）。

### 10.2 带模型 + zip

```powershell
.\scripts\build-release.ps1 -Mode self-contained -IncludeModels -Zip
```

### 10.3 完整离线包（约 2.7 GB）

```powershell
.\scripts\pack-ready.ps1
```

会清理 `dist/`、构建、可选刷新 wake encodings、打 zip。详见 [`docs/DEPLOY_SHERPA.md`](DEPLOY_SHERPA.md)。

### 10.4 framework-dependent 小包

目标机需预装 .NET 8 Desktop Runtime：

```powershell
.\scripts\build-release.ps1 -Mode framework-dep
```

---

## 11. Sherpa-ONNX 原生依赖

无需手动拷贝 DLL：NuGet 包 `org.k2fsa.sherpa.onnx`（当前 **1.13.2**）+ `org.k2fsa.sherpa.onnx.runtime.win-x64` 在 build/publish 时复制 native 库到输出目录。

详情与故障排查：[`docs/DEPLOY_SHERPA.md`](DEPLOY_SHERPA.md)。

---

## 12. 从另一台电脑迁移开发环境

### 12.1 推荐方式（干净）

1. 新机器安装 .NET 8 SDK + Git  
2. `git clone` 仓库  
3. `.\scripts\download-models.ps1 -Package all -IncludeKws`  
4. `dotnet build` / `dotnet test` / `dotnet run`  

### 12.2 快速方式（拷贝模型）

1. git clone  
2. 从旧机器复制整个 `models\` 到仓库根  
3. （可选）复制 `%AppData%\ArrayMicRefreshment\` 以保留设置与声纹 enrollment  

### 12.3 不要依赖拷贝的内容

- `bin/`、`obj/`、`dist/` — 在新机器重新 build  
- 另一台机器的 `%AppData%` — 含 API Key，勿提交 git；迁移时注意保密  

---

## 13. 常见问题

| 现象 | 可能原因 | 处理 |
|------|----------|------|
| 启动后无 ASR，托盘提示模型 | `models/` 为空 | 运行 `download-models.ps1` |
| 唤醒词从不触发 | 未下 KWS；或 keywords 用了 `#0.30` 高阈值 | `-IncludeKws`；装 Python 跑 `generate-wake-encodings.ps1`；查 `[WAKE-DIAG]` |
| 识别成功后再唤醒要等很久 | KWS 流/AGC 状态；关键词阈值 | 查日志 `RearmAfterDictation`、`lifetimeHits`；见近期 wake 相关 commit |
| 声纹明明够分仍拒绝 | 自适应阈值高于用户阈值 | 查日志 `effectiveThreshold`；确认最新 `UserEnrollmentService` |
| PTT 吞字 | 采集启动晚于 UI | 查 `PttCaptureService` handoff；WASAPI buffer 20ms |
| `dotnet build` WinForms 失败 on Linux | 全 sln 含 App | 改用 `ArrayMicRefreshment.CI.slnf` |
| 打包时 `Remove-Item dist` 失败 | exe 仍在运行 | `Stop-Process -Name ArrayMicRefreshment` 后重试 |
| `tar` 解压失败 | 旧 Windows 无 tar | Windows 10 1803+ 自带 tar |

---

## 14. Agent 协作说明

供 Cursor Agent 在本仓库改代码时参考。

### 14.1 改代码前

1. 读本文档完成 build 环境  
2. 改 wake/PTt/声纹时，先读相关测试：`tests/ArrayMicRefreshment.Core.Tests/`、`tests/ArrayMicRefreshment.Audio.Tests/`  
3. 产品决策与架构背景见 [`README.md`](../README.md)

### 14.2 改代码后

```powershell
dotnet build ArrayMicRefreshment.sln -c Release
dotnet test ArrayMicRefreshment.sln -c Release --filter "FullyQualifiedName!~Integration"
```

Windows 实机验证：PTT、唤醒、声纹、Both 模式各测一遍。

### 14.3 日志分析

```powershell
Select-String -Path "$env:APPDATA\ArrayMicRefreshment\logs\app-*.log" -Pattern "\[WAKE-DIAG\]"
```

关注：`lifetimeHits`、`keywordLine`、`Loud input without wake`、`Speaker gate`、`RearmAfterDictation`。

### 14.4 不要提交

- `models/`、`dist/`、`*.zip`、`.env`、含 API Key 的 `settings.json`  
- 用户声纹 enrollment  

### 14.5 相关文档索引

| 文档 | 内容 |
|------|------|
| [`README.md`](../README.md) | 产品架构与决策 |
| [`docs/DEPLOY_SHERPA.md`](DEPLOY_SHERPA.md) | Sherpa 与模型部署 |
| [`docs/ASR_MODEL.md`](ASR_MODEL.md) | SenseVoice 选型 |
| [`docs/SKILL_PIPELINE.md`](SKILL_PIPELINE.md) | LLM 整理管线 |
| [`CHANGELOG.md`](../CHANGELOG.md) | 版本变更 |

---

## 15. 一键脚本（复制整段）

将下列内容存为 `setup-dev.ps1` 可在新机器批量执行（需已装 Git 与 .NET 8 SDK）：

```powershell
$ErrorActionPreference = "Stop"
$repo = "array-mic-refreshment"
if (-not (Test-Path $repo)) {
    git clone https://github.com/wangyuanzhong/array-mic-refreshment.git $repo
}
Set-Location $repo
dotnet --version
.\scripts\download-models.ps1 -Package all
.\scripts\download-models.ps1 -IncludeKws
dotnet build ArrayMicRefreshment.sln -c Release
dotnet test ArrayMicRefreshment.sln -c Release --filter "FullyQualifiedName!~Integration"
Write-Host "Done. Run: dotnet run --project src\ArrayMicRefreshment.App -c Release" -ForegroundColor Green
```

---

*文档维护：环境或脚本变更时请同步更新本节与 [`README.md`](../README.md) 文档索引。*
