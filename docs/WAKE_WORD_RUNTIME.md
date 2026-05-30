# Wake-word runtime architecture (Agent B)

Branch: `cursor/wakeword-core-arch-91a6`

## Production-ready (safe to ship behind feature flag)

| Component | Role |
|-----------|------|
| `VoiceTriggerMode` / `VoiceTriggerKind` | Runtime routing enums |
| `IWakeWordDetector` | Pluggable KWS contract |
| `IWakeWordCaptureService` | Wake path capture contract |
| `VoiceCaptureOrchestrator` | Mode switch, PTT vs wake forwarding, PTT priority when held |
| `WakeWordCaptureService` | Listen → dictation session → `AudioUtterance` (same finalize/resample as PTT) |
| `TrayApplicationContext` | Orchestrator wiring, shared `VoicePipeline`, tray **runtime** mode menu (not settings form) |

## Skeleton / stub (replace before user-facing wake word)

| Component | Limitation |
|-----------|------------|
| `StubWakeWordDetector` | No Sherpa KWS; manual `SimulateDetection()` or chunk-count auto-fire for tests |
| `WakeWordCaptureService` continuous listen | Opens a real mic stream but feeds stub detector only |
| Session end | Built-in **700ms** pause after speech; **Sherpa Silero VAD** when `models/silero_vad.onnx` exists; energy gap fallback if missing (not user-configurable) |
| Mode persistence | Tray menu only; not saved to `settings.json` |

## Next agent (KWS / settings UI)

1. Implement `SherpaKeywordSpotter : IWakeWordDetector` with models in `ModelManifest.json`.
2. Persist `VoiceTriggerMode` and wake phrase in `AppSettings` + settings form.
3. Single shared mic session to avoid listen/dictation stream hand-off races on Windows.

## Verify (Linux CI)

```bash
dotnet test ArrayMicRefreshment.CI.slnf --configuration Release
```

## Verify (Windows)

```powershell
dotnet test ArrayMicRefreshment.sln --configuration Release
dotnet run --project src/ArrayMicRefreshment.App
```

Tray → **触发模式（运行时）** → switch mode; **模拟唤醒** exercises stub path into the same pipeline as PTT.
