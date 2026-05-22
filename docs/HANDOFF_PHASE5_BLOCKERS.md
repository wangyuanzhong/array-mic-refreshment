# Phase 5 — Blockers / partial completion notes

## Tasks 5–8 (GUI demos) — partial

Automated capture (`scripts/record-phase5-demos.ps1`) produced tray smoke screenshots and slideshow MP4s under `docs/demos/`. **Live** requirements were not fully met in this agent session:

| Task | Status | Notes |
|------|--------|-------|
| 5 Tray GUI | ⚠️ Partial | App started; screenshots captured. `dotnet run` cwd resolves `models/` under `bin/…`, so log showed stub pipeline unless settings point to repo `models/` absolute path. No manual tray menu / settings walkthrough on video. |
| 6 PTT → ASR → clipboard | ⚠️ Partial | `demo_ptt_asr.mp4` is a placeholder slideshow (same frames as tray smoke). Real mic PTT + Notepad paste not recorded. Integration test `SenseVoice_real_model_decodes_when_enabled` validates ASR with real model. |
| 7 LLM refine | ⚠️ Partial | `demo_refine.mp4` placeholder. Ollama not installed (`winget` unavailable in shell). |
| 8 Enrollment | ⚠️ Partial | `screenshot_enrollment_files.png` shows demo JSON under `%APPDATA%\ArrayMicRefreshment\speakers\`. `demo_enrollment.mp4` placeholder; `EnrollmentDialog` 3-sample flow not recorded. |

## Environment fixes applied during handoff

- Installed .NET 8 SDK to `%USERPROFILE%\.dotnet` (not on PATH by default in this shell).
- Downloaded SenseVoice model (~226 MB) via `scripts/download-models.ps1`.

## Task 9 CER caveat

CER used **Windows TTS** (`System.Speech`) as input, not human microphone. English / code-mixed lines show high CER (mean **72%**); see `docs/CER_BASELINE.md`. Conclusion recommends Qwen3-ASR evaluation for code terms.
