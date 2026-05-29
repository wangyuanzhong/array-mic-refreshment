[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path
)

# apply-amr-cursor-overlays.ps1 — append project-specific sections after universal rule sync
# Does NOT replace universal 00-universal-core / exe-packaging bodies (would weaken the pack).

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
| CI 排错 | `.cursor/skills/github-actions-ci/` → `/github-actions-ci` |

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

Write-Host '→ Applying AMR overlays (append-only)' -ForegroundColor Cyan
Add-SectionIfMissing (Join-Path $rules '00-universal-core.mdc') '## This repo (array-mic-refreshment)' $coreSection
Add-SectionIfMissing (Join-Path $rules 'exe-packaging-local-cloud.mdc') '## This repo (array-mic-refreshment)' $exeSection
Write-Host '✔ AMR overlays done' -ForegroundColor Green
