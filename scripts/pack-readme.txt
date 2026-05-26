Array Mic Refreshment - offline package
========================================

1. Extract ArrayMicRefreshment.zip to any folder (e.g. D:\ArrayMicRefreshment).
2. Run ArrayMicRefreshment.exe (no .NET install required).

If Windows Explorer fails to extract, use PowerShell in the target folder:

  Expand-Archive .\ArrayMicRefreshment.zip -DestinationPath . -Force

Wake word (Settings -> trigger mode -> wake word):
  Supported phrases include: xiao zhu shou / xiao de xiao de (Chinese: see app UI).
  Custom Chinese phrases need encoding; use presets or run scripts/generate-wake-encodings.ps1 when building.

Need ~6 GB free disk space.
