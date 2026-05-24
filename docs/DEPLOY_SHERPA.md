# Sherpa-ONNX 原生库部署（v0.1）

ASR（SenseVoice）与说话人门禁（Speaker embedding）均通过 NuGet 包 **`org.k2fsa.sherpa.onnx`** 调用 Sherpa-ONNX C API。Windows 托盘应用与 Linux CI 类库共用同一包；native 运行时由配套 runtime 包自动还原。

## NuGet 与 native 运行时

| 包 | 作用 |
|----|------|
| `org.k2fsa.sherpa.onnx` | 托管绑定 `SherpaOnnx`（`OfflineRecognizer`、`SpeakerEmbeddingExtractor` 等） |
| `org.k2fsa.sherpa.onnx.runtime.win-x64` 等 | 按 RID 附带 `sherpa-onnx-c-api` / `onnxruntime` 等 native DLL |

`dotnet build` / `dotnet publish` 后，native 库会出现在输出目录，例如：

```text
bin/Release/net8.0/runtimes/win-x64/native/
  sherpa-onnx-c-api.dll
  onnxruntime.dll
  ...
```

Linux CI（`scripts/build-libraries.sh`）在 `linux-x64` 下同样会还原 `runtimes/linux-x64/native/`，**无需**在仓库中提交 DLL。

## 语音模型（ASR + Speaker）

由 [`scripts/download-models.ps1`](../scripts/download-models.ps1) 下载到 `models/`（已 gitignore）：

```powershell
.\scripts\download-models.ps1
.\scripts\download-models.ps1 -Package all
.\scripts\download-models.ps1 -IncludeSpeaker
```

| 角色 | 目录 / 文件 |
|------|-------------|
| ASR 主包 | `models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/` |
| ASR 回退 | `models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/` |
| Speaker | `models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k/*.onnx` |

应用通过 `AppSettings.ModelsDirectory`（默认 `models`）解析上述路径；缺失时托盘回退 stub 并提示运行 `download-models.ps1`。

## 手动拷贝 native DLL（仅当 NuGet 未复制时）

1. 从 [sherpa-onnx releases](https://github.com/k2-fsa/sherpa-onnx/releases) 下载 **win-x64** 预编译包。
2. 将 `sherpa-onnx-c-api.dll` 及依赖复制到：
   - `src/ArrayMicRefreshment.App/runtimes/win-x64/native/`，或
   - 发布目录 `runtimes/win-x64/native/`。
3. 确保与 `org.k2fsa.sherpa.onnx` 版本匹配（当前仓库锁定 **1.13.2**）。

## 本机开发（Windows）

```powershell
.\scripts\download-models.ps1
.\scripts\download-models.ps1 -IncludeSpeaker
dotnet build src\ArrayMicRefreshment.App\ArrayMicRefreshment.App.csproj -c Release
dotnet run --project src\ArrayMicRefreshment.App -c Release
```

## Linux / CI（类库 + 测试）

```bash
dotnet restore ArrayMicRefreshment.CI.slnf
./scripts/build-libraries.sh
dotnet test tests/ArrayMicRefreshment.Core.Tests -c Release
```

> **说明**：WinForms 托盘应用需在 **Windows** 上运行。CI 不下载模型；单元测试通过 mock recognizer / embedding 后端验证逻辑。

## 完整离线包（v0.1）

在已下载 `models/` 的机器上：

```powershell
.\scripts\build-release.ps1 -Mode self-contained -IncludeModels -Zip
```

将 `dist\ArrayMicRefreshment-self-contained\` 与 `models\`、`skills\` 一并打包为可分发的 `ArrayMicRefreshment-ready.zip`（约 2.7 GB，含三套 SenseVoice + 声纹模型）。用户解压后无需再运行 `download-models.ps1`。
