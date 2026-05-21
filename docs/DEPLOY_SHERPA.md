# Sherpa-ONNX 原生库部署（Phase 0 说明）

首版 ASR / VAD / Speaker 均通过 **Sherpa-ONNX** C API 调用。Windows 上需随应用分发或首次启动解压 native DLL。

## 推荐步骤（实现 Phase 3 时落地）

1. 从 [sherpa-onnx releases](https://github.com/k2-fsa/sherpa-onnx/releases) 下载 **win-x64** 预编译包（或自行构建）。
2. 将 `sherpa-onnx-c-api.dll` 及依赖（`onnxruntime.dll` 等）复制到：
   - `src/ArrayMicRefreshment.App/runtimes/win-x64/native/`  
   或安装目录 `native/`.
3. C# 侧通过 P/Invoke 调用官方 C# 示例中的 `SherpaOnnx` 绑定（与 `ArrayMicRefreshment.Asr` 项目同仓封装）。
4. 语音模型由 [`scripts/download-models.ps1`](../scripts/download-models.ps1) 下载到 `models/`（见 [`ModelManifest.json`](../scripts/ModelManifest.json)）。

## 本机开发

```powershell
.\scripts\download-models.ps1
dotnet build src\ArrayMicRefreshment.App\ArrayMicRefreshment.App.csproj -c Release
dotnet run --project src\ArrayMicRefreshment.App -c Release
```

> **说明**：托盘 WinForms 应用需在 **Windows** 上构建运行。Linux CI 仅编译 `ArrayMicRefreshment.Core` 等类库项目。
