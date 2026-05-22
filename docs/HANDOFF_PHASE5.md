# Phase 5 — 本地 Agent 收尾工作清单

> **本文档面向本地 Cursor agent（Windows 环境）独立完成，不需要用户做任何手工操作。**
>
> 把这份文档发给本地 agent，让它直接照做。所有步骤都给出了具体命令、验收标准、artifact 路径、commit/PR 要求。

---

## 0. 前置环境（agent 自检）

1. 必须在 **Windows 10/11** 上跑（验证 NAudio、SendInput、Sherpa 真模型）。
2. 必须存在的工具（缺啥装啥，不要问用户）：
   - .NET 8 SDK：`dotnet --version` ≥ 8.0.x
   - PowerShell 5.1+（Windows 自带）
   - Git（已配置用户名邮箱，仓库已克隆）
   - 一个**真实的录音设备**（用于真机 PTT 测试）
   - 互联网（下载 ~230 MB SenseVoice 模型）
3. 仓库根目录 `cd <repo-root>`，确保在 main 最新：
   ```powershell
   git checkout main
   git pull --ff-only origin main
   ```
4. 创建工作分支：
   ```powershell
   git checkout -b cursor/phase5-finalization-ead1
   ```
   分支名必须 `cursor/` 前缀、`-ead1` 后缀、全部小写。仓库已配置 auto-PR workflow，**push 后 GitHub Actions 会自动开 draft PR**，无须手动创建。

---

## 1. 任务清单（按顺序执行）

### Task 1 — Sherpa native DLL 部署验证（5 分钟）

**目的**：确认 `dotnet publish` 与 `dotnet run` 产出的 App 输出目录里有 `sherpa-onnx-c-api.dll`、`onnxruntime.dll` 等 native lib。

```powershell
dotnet publish src\ArrayMicRefreshment.App -c Release -r win-x64 --self-contained false -o publish-test
Get-ChildItem publish-test -Recurse -Filter "*.dll" | Where-Object { $_.Name -match "sherpa|onnxruntime" } | Select-Object Name, Directory
```

**验收**：至少能看到 `sherpa-onnx-c-api.dll` + `onnxruntime.dll`，否则修复 `src/ArrayMicRefreshment.App/ArrayMicRefreshment.App.csproj` 或 `src/ArrayMicRefreshment.Asr/ArrayMicRefreshment.Asr.csproj`，加显式 `<None Include="...runtimes\win-x64\native\*.dll"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`，再 publish 直到 DLL 出现。

把 `publish-test/` 加入 `.gitignore`（如果还没在）。

---

### Task 2 — 下载 SenseVoice 模型（一次性，~3 分钟）

```powershell
.\scripts\download-models.ps1
```

**验收**：`models\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09\model.int8.onnx` 文件存在且 > 200 MB。如果脚本失败（网络/版本），把错误日志写进 `docs/HANDOFF_PHASE5_NOTES.md`，**回退到** `2024-07-17` 版本继续。

模型文件**不要 commit**（`.gitignore` 已有 `models/`）。

---

### Task 3 — 真模型集成测试解锁（5 分钟）

```powershell
$env:INTEGRATION_REAL_MODELS = "1"
dotnet test tests\ArrayMicRefreshment.Integration.Tests -c Release --filter "FullyQualifiedName~SherpaRealModelTests" --verbosity normal
```

**验收**：`SenseVoice_real_model_decodes_when_enabled` 不再走 skip 分支，而是真的调 SenseVoice 解码并断言文本非空。如果失败：
- 检查 `tests/ArrayMicRefreshment.Integration.Tests/Resources/short.wav` 是否存在。**没有就生成一个**：用 PowerShell + `System.Speech.Synthesis` 合成一句"你好 测试 ASR"到 `Resources/short.wav`（16 kHz, mono, 16-bit PCM），加进 csproj `<EmbeddedResource>` 或 `<Content CopyToOutputDirectory="PreserveNewest">`。
- 修任何 path / format 问题，确保测试在有模型时真的跑、能解码、能 assert。

然后跑全套集成测试确保没有回归：
```powershell
$env:INTEGRATION_REAL_MODELS = "1"
dotnet test tests\ArrayMicRefreshment.Integration.Tests -c Release --verbosity normal
```
**验收**：10/10 全 pass（之前 SherpaRealModelTests 走 skip 分支算 pass；现在它真的解码也算 pass）。

---

### Task 4 — 修两个遗留 xUnit warning（2 分钟）

`SherpaRealModelTests.cs` 和 `OnRefineFailureMatrixTests.cs` 里有 `await xxx.ConfigureAwait(false)` 在测试方法体里，触发 `xUnit1030`。去掉 `.ConfigureAwait(false)` 即可（任务体里其他 awaits 之前 Task 6 round 3 已修过，这两个漏网）。

---

### Task 5 — App 启动 smoke + 托盘 GUI 验证（10 分钟，录视频）

启动屏幕录制：用 Windows + Alt + R（Xbox Game Bar）或 OBS，录制下面整段流程到一个文件 `C:\demos\demo_tray_basic.mp4`。

```powershell
dotnet run --project src\ArrayMicRefreshment.App -c Release
```

**录制要点（按顺序）**：
1. 系统托盘出现 Array Mic 图标，hover 显示"Array Mic — 转写:开 粘贴:关 整理:关"
2. 右键托盘 → 菜单含「启用语音转写 ✓」「识别后粘贴到光标」「状态: 就绪」「设置…」「模拟松开 PTT」「退出」
3. 点「设置…」→ 设置窗打开，能看到全部新控件：API URL/Key/Model、整理 checkbox、PTT 热键、强制意图、整理失败时、**录音设备下拉（含真实设备列表）**、**当前用户下拉 + 新增/删除按钮**、**附加叠加 skill 多选**、**测试连接按钮**
4. 关闭设置窗、点托盘「退出」干净退出
5. 检查日志 `%APPDATA%\ArrayMicRefreshment\logs\app-*.log` — 不能有未捕获 exception

**验收**：录像清晰展示以上 5 步；如出现 NRE / crash，先修 bug 再录。

提交录像到 `docs/demos/demo_tray_basic.mp4`（先检查文件 < 50 MB；如太大用 ffmpeg 压到 720p 30fps）。

---

### Task 6 — PTT → 真 SenseVoice ASR → 剪贴板（核心 demo，必录）

开新录制 `C:\demos\demo_ptt_asr.mp4`。

1. 启动 App：`dotnet run --project src\ArrayMicRefreshment.App -c Release`
2. 打开记事本作为粘贴目标
3. 托盘 → 开启「识别后粘贴到光标」
4. 按住默认 PTT 热键 `Ctrl+Shift+Space`，对麦克风**清晰**说一句中文，例如"你好 这是 Array Mic 测试"
5. 松开热键
6. **预期**：~1-3 秒内文本出现在记事本（自动粘贴）+ 剪贴板含同样文本
7. 关 paste，再按一次 PTT 说英文"Hello world this is a test"
8. 在记事本里 Ctrl+V 手动粘贴，验证剪贴板内容正确
9. 同时观察托盘状态行从「状态: 识别中…」回到「状态: 就绪」

**验收**：录像显示至少 2 段不同语言识别成功，文本合理（允许个别字错）。

提交 `docs/demos/demo_ptt_asr.mp4`。

---

### Task 7 — LLM 提示词整理 demo（用本地 Ollama 或 OpenAI Key）

如果**没有 OpenAI Key**：
1. 安装 Ollama（`winget install Ollama.Ollama`），拉一个小模型 `ollama pull qwen2:1.5b`，`ollama serve`。
2. 设置窗里：
   - API Base URL = `http://127.0.0.1:11434/v1`
   - API Key 留空
   - Model = `qwen2:1.5b`
   - 勾选「启用提示词整理」
   - 强制意图 = CodeEditing

如果**有 OpenAI Key**：用 `https://api.openai.com/v1` + 真 key + `gpt-4o-mini`。设置时**期望弹隐私确认对话框**，验证后再按 PTT。

开录制 `C:\demos\demo_refine.mp4`：
1. 按 PTT 说"创建一个 React 按钮组件 支持 disabled 和 loading 状态"
2. 松开
3. **预期**：剪贴板里不是 ASR 原文，而是被整理过的 / 增强过的指令格式（比如带"Please create a React component..."等结构化 prompt）
4. 切到 Notepad++ 粘贴对比

**验收**：录像清晰显示「原文 vs 整理后」差异；隐私弹窗在使用 OpenAI 时真的出现。

提交 `docs/demos/demo_refine.mp4`。

---

### Task 8 — Speaker Enrollment demo（GUI + 行为验证）

开录制 `C:\demos\demo_enrollment.mp4`：
1. 设置窗 → 点「新增用户…」→ `EnrollmentDialog` 弹出
2. 输入名字"Owner"
3. 按引导录 3 段（每段 3-5 秒，随便说话）
4. 对话框完成、用户被设为 Current
5. 回设置窗，"当前用户"下拉现在含 "Owner"
6. 「OK」保存
7. **行为测试**：托盘按 PTT 说话，应正常识别
8. 切回设置窗，"当前用户"下拉选回「无」或新加一个"Other"用户但不录入足够样本
9. 再按 PTT —— 验证日志 / sink 行为符合 SpeakerGate 设计（无 enrollment 时放行 + Log.Warning；有 enrollment 但 mismatch 时不放行）
10. enrollment 文件落到 `%APPDATA%\ArrayMicRefreshment\speakers\*.json`，截图文件夹

**验收**：录像 + 一张文件夹截图 `docs/demos/screenshot_enrollment_files.png`。

---

### Task 9 — CER 实测评估（Phase 5 关键交付）

写一个 PowerShell + .NET 小脚本 `scripts/Measure-Cer.ps1`，对一组**代码相关术语**和**通用句**测量 CER：

测试集（写在 `scripts/cer-test-set.json`）：
```json
[
  {"text": "ApiService 调用 async await 方法", "lang": "zh-en-mix"},
  {"text": "请帮我创建一个 React 按钮", "lang": "zh"},
  {"text": "git checkout main and pull latest", "lang": "en"},
  {"text": "useEffect 依赖数组要写完整", "lang": "zh-en-mix"},
  {"text": "TypeScript interface vs type alias", "lang": "en"},
  {"text": "请检查 null reference exception", "lang": "zh-en-mix"},
  {"text": "Open the settings dialog and save", "lang": "en"},
  {"text": "数据库迁移用 migration 命令", "lang": "zh-en-mix"},
  {"text": "this dot props dot children", "lang": "en"},
  {"text": "异步 promise then catch finally", "lang": "zh-en-mix"}
]
```

脚本流程：
1. 对每条文本，用 Windows TTS 合成 16 kHz mono PCM（或让 agent 自己录），文件存 `scripts/cer-audio/<idx>.wav`
2. 调 `SenseVoiceAsr.RecognizeUtteranceAsync(...)` 解码
3. 计算 CER = `Levenshtein(预测, 标准) / Length(标准)`
4. 输出 `docs/CER_BASELINE.md`：表格列「标准文本 / 识别结果 / CER%」+ 总体均值 + 结论段（若代码术语 CER > 25%，建议 README 提到日后切 Qwen3-ASR）

**验收**：`docs/CER_BASELINE.md` 存在且数字看起来合理（中文 CER 大概 < 15%，纯英代码术语可能 30%+）。

---

### Task 10 — 更新 README，标记 Phase 5 完成

打开 `README.md`，更新：

- Phase 5 复选框：`[ ]` → `[x]`，并加链接到 `docs/CER_BASELINE.md` 和 demo 视频
- 「分阶段实施计划」段落最末加一行：
  ```
  *Phase 5 收尾验证由本地 agent 在 Windows 上完成；demo 视频在 docs/demos/，CER 评估在 docs/CER_BASELINE.md。*
  ```
- 如果实测发现代码术语 CER 高，把这条加进未来工作建议

---

### Task 11 — 提交 + 推送 + 自动开 PR

```powershell
git add -A
git status                                  # 应该不含 models/、publish-test/
git commit -m "phase5: native deploy verified, real model E2E, CER baseline, demos"
git push -u origin cursor/phase5-finalization-ead1
```

**自动 PR**：仓库的 `.github/workflows/auto-open-pr.yml` 会在 push 后自动开 draft PR。等待 ~30 秒，运行 `gh pr view --json url` 拿到 URL（如 gh 在本地未登录，给出 GitHub URL：`https://github.com/wangyuanzhong/array-mic-refreshment/pulls`，由用户在 web 上 review + merge）。

**PR description** 必须包含：
- Task 1-9 每项的验收状态（✅/⚠️/❌）
- 关键截图 + 视频链接（用相对路径 `docs/demos/*.mp4`）
- CER 总体数字
- 任何已知 issue 留给后续

PR 不要自己 merge，留给用户审阅；用户**唯一要做的就是点 Merge 按钮**。

---

## 2. 硬性约束（agent 必须遵守）

- 不删 / 不改任何已有 `src/` 业务接口签名（追加 OK）
- 不动 `tests/ArrayMicRefreshment.Core.Tests` / `Audio.Tests` / `Prompt.Tests` 的现有 case（追加 OK）
- 不动 `.github/workflows/auto-open-pr.yml`、`.github/workflows/ci.yml`（除非 Task 1 需要新增 Windows job 步骤，但默认不需要）
- 不上传 `models/*` 或 `publish-test/*` 到 git
- 视频文件 < 50 MB；如超出，用 `ffmpeg -i input.mp4 -vf scale=-2:720 -r 30 -c:v libx264 -crf 28 out.mp4` 压缩
- 任何步骤失败：先尝试自己修，修不了把详细 log 写进 `docs/HANDOFF_PHASE5_BLOCKERS.md` 一并 commit，PR 标题加 `[partial]` 前缀

---

## 3. 一次性命令（如果 agent 想一把梭，参考）

```powershell
# 完整执行
git checkout main; git pull --ff-only origin main
git checkout -b cursor/phase5-finalization-ead1

# Task 1
dotnet publish src\ArrayMicRefreshment.App -c Release -r win-x64 --self-contained false -o publish-test

# Task 2
.\scripts\download-models.ps1

# Task 3
$env:INTEGRATION_REAL_MODELS = "1"
dotnet test tests\ArrayMicRefreshment.Integration.Tests -c Release --verbosity normal

# Task 5-8: 录屏（需 GUI 交互）
dotnet run --project src\ArrayMicRefreshment.App -c Release

# Task 9: CER
pwsh -File scripts\Measure-Cer.ps1

# Task 11
git add -A
git commit -m "phase5: native deploy verified, real model E2E, CER baseline, demos"
git push -u origin cursor/phase5-finalization-ead1
```

---

## 4. 全部完成的判定条件

- ✅ Task 1-10 全部完成或在 BLOCKERS 文档里有清晰说明
- ✅ `docs/demos/` 至少含 `demo_tray_basic.mp4`、`demo_ptt_asr.mp4`、`demo_refine.mp4`、`demo_enrollment.mp4`
- ✅ `docs/CER_BASELINE.md` 含具体数字表
- ✅ README Phase 5 标 `[x]`
- ✅ 分支已 push，draft PR 已被 auto-pr workflow 创建
- ✅ 用户**只需要打开 PR 点 Merge**，无其他手工操作
