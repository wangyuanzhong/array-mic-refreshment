[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path
)

# apply-amr-cursor-overlays.ps1 — append project-specific sections after universal rule sync
# Does NOT replace universal rule bodies (would weaken the pack).

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$rules = Join-Path $ProjectRoot '.cursor/rules'

function Add-SectionIfMissing {
    param([string]$Path, [string]$Marker, [string]$Section)
    if (-not (Test-Path $Path)) { return }
    $content = Get-Content $Path -Raw -Encoding UTF8
    if ($content -match [regex]::Escape($Marker)) {
        Write-Host "  · $(Split-Path $Path -Leaf): AMR section already present" -ForegroundColor DarkGray
        return
    }
    Add-Content -Path $Path -Value "`n$Section" -Encoding UTF8 -NoNewline
    Write-Host "  ✔ $(Split-Path $Path -Leaf): appended AMR section" -ForegroundColor Green
}

$coreSection = @'

## This repo (array-mic-refreshment)

Also read [`AGENTS.md`](../../AGENTS.md) and [`docs/LOCAL_DEVELOPMENT.md`](../../docs/LOCAL_DEVELOPMENT.md).

| Topic | Where |
|-------|--------|
| 路线 B / WebView2 | [`docs/UI_ROUTE_B_WEBVIEW2.md`](../../docs/UI_ROUTE_B_WEBVIEW2.md) |
| Windows 自动化测试 | `.\scripts\test-phase2-route-b.ps1`、`.\scripts\test-feature-presets.ps1` |
| UI 视觉 | `.cursor/skills/frontend-design/` → `/frontend-design` |
| CI 排错 | [`post-push-ci-green.mdc`](post-push-ci-green.mdc)（含本仓库 workflow 表与常见坑） |

**Windows CI is mandatory for this EXE repo:** `ci.yml` job `build-windows` (App.Tests) **and** `build-release-exe.yml` must pass. Ubuntu-only green is not enough.
'@

$exeSection = @'

## This repo (array-mic-refreshment)

**Active** — `scripts/watch-build-release.ps1` and `scripts/build-release.ps1` exist.

| Workflow | File | Purpose |
|----------|------|---------|
| CI | `.github/workflows/ci.yml` | `build-and-test` (Linux) + **`build-windows`** (App.Tests) |
| Release EXE | `.github/workflows/build-release-exe.yml` | `build-release.ps1` on `windows-latest` |

Release build also runs `ui/` → `npm ci && npm run build` before publish. Log: `dist\watch-build.log`. If `dist/` locked, stop `ArrayMicRefreshment` process first.
'@

$postPushSection = @'

## This repo (array-mic-refreshment)

| Workflow file | Name | Runner | Notes |
|---------------|------|--------|-------|
| `.github/workflows/ci.yml` | CI | `ubuntu-latest` + `windows-latest` | Windows **App.Tests** |
| `.github/workflows/build-release-exe.yml` | Build release EXE | `windows-latest` | `build-release.ps1`; robocopy → `$LASTEXITCODE = 0` |
| `.github/workflows/release.yml` | Release | tags | Manual |

### Common pitfalls (this repo)

- **Intent router tests**: `skills/manifest.yaml` `intent_map` keys (e.g. `general_chat`, not `general_ai`).
- **WebUiBridge JSON**: `WhenWritingNull`; no `"warning": null` in success JSON.
- **Windows-only** App.Tests — Ubuntu green ≠ done.
- **dotnet --filter**: separate test steps on Windows CI; no `&` in one shell line.
- **Job stuck on App.Tests for hours**: usually `vstest` never exits (leaked WinForms `Timer` / undisposed PTT). Fix tests to use stub PTT; CI uses `--blame-hang-timeout` and job `timeout-minutes`.

### Optional: local pre-push

```bash
dotnet test ArrayMicRefreshment.CI.slnf -c Release
dotnet test tests/ArrayMicRefreshment.App.Tests/ArrayMicRefreshment.App.Tests.csproj -c Release  # Windows
```
'@

Write-Host '→ Applying AMR overlays (append-only)' -ForegroundColor Cyan
Add-SectionIfMissing (Join-Path $rules '00-universal-core.mdc') '## This repo (array-mic-refreshment)' $coreSection
Add-SectionIfMissing (Join-Path $rules 'exe-packaging-local-cloud.mdc') '## This repo (array-mic-refreshment)' $exeSection
Add-SectionIfMissing (Join-Path $rules 'post-push-ci-green.mdc') '## This repo (array-mic-refreshment)' $postPushSection
Write-Host '✔ AMR overlays done' -ForegroundColor Green
