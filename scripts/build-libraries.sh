#!/usr/bin/env bash
# Build cross-platform class libraries (Linux CI / macOS). WinForms App requires Windows.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
projects=(
  "$root/src/ArrayMicRefreshment.Core/ArrayMicRefreshment.Core.csproj"
  "$root/src/ArrayMicRefreshment.Audio/ArrayMicRefreshment.Audio.csproj"
  "$root/src/ArrayMicRefreshment.Speaker/ArrayMicRefreshment.Speaker.csproj"
  "$root/src/ArrayMicRefreshment.Asr/ArrayMicRefreshment.Asr.csproj"
  "$root/src/ArrayMicRefreshment.Prompt/ArrayMicRefreshment.Prompt.csproj"
  "$root/src/ArrayMicRefreshment.Output/ArrayMicRefreshment.Output.csproj"
)
for p in "${projects[@]}"; do
  echo "Building $p"
  dotnet build "$p" -c Release --nologo -v q
done
echo "Library build OK."
