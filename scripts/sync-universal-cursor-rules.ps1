[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path,

    [string]$UniversalRepo = '',

    [switch]$Refresh,

    [switch]$WhatIf
)

# sync-universal-cursor-rules.ps1 — refresh .cursor/rules from cursor-universal-rule
#
#   .\scripts\sync-universal-cursor-rules.ps1
#   .\scripts\sync-universal-cursor-rules.ps1 -Refresh
#   .\scripts\sync-universal-cursor-rules.ps1 -UniversalRepo C:\src\cursor-universal-rule

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$cloneUrl = 'https://github.com/wangyuanzhong/cursor-universal-rule.git'

if (-not $UniversalRepo) {
    $UniversalRepo = Join-Path $env:TEMP 'cursor-universal-rule'
}

if ($Refresh -or -not (Test-Path (Join-Path $UniversalRepo 'rules'))) {
    Write-Host "→ Cloning cursor-universal-rule to $UniversalRepo ..." -ForegroundColor Cyan
    if (-not $WhatIf) {
        if (Test-Path $UniversalRepo) { Remove-Item $UniversalRepo -Recurse -Force }
        git clone --depth 1 $cloneUrl $UniversalRepo
    }
}

$installer = Join-Path $UniversalRepo 'scripts\install-universal-rules.ps1'
if (-not (Test-Path $installer)) {
    throw "install-universal-rules.ps1 not found under $UniversalRepo"
}

Write-Host "→ Installing universal rules into $ProjectRoot" -ForegroundColor Cyan
$installArgs = @{
    ProjectRoot   = $ProjectRoot
    UniversalRepo = $UniversalRepo
    FixGitignore  = $true
}
if ($WhatIf) { $installArgs['WhatIf'] = $true }

& $installer @installArgs

if (-not $WhatIf) {
    $sha = (git -C $UniversalRepo rev-parse HEAD).Trim()
    $lockPath = Join-Path $ProjectRoot '.cursor\UNIVERSAL_RULE_LOCK'
    @(
        "cursor-universal-rule=$sha"
        "synced=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK')"
        "url=https://github.com/wangyuanzhong/cursor-universal-rule"
    ) | Set-Content -Path $lockPath -Encoding UTF8
    Write-Host "  ✔ .cursor/UNIVERSAL_RULE_LOCK ($sha)" -ForegroundColor Green

    $overlays = Join-Path $PSScriptRoot 'apply-amr-cursor-overlays.ps1'
    & $overlays -ProjectRoot $ProjectRoot

    # Restore project README (universal installer may overwrite with generic)
    $readmeSrc = Join-Path $ProjectRoot '.cursor\README.md'
    if (Test-Path $readmeSrc) {
        $body = Get-Content $readmeSrc -Raw -Encoding UTF8
        if ($body -match 'local-skip-post-push-ci') {
            Write-Host '  ⚠ .cursor/README.md still mentions local-skip — re-run apply-amr-cursor-overlays or restore from git' -ForegroundColor Yellow
        }
    }

    # Merge github-actions-ci skill: universal + AMR
    $skillPath = Join-Path $ProjectRoot '.cursor\skills\github-actions-ci\SKILL.md'
    $amrTail = @'

## Workflows in this repo (array-mic-refreshment)

| Workflow file | Name | Runner | Notes |
|---------------|------|--------|-------|
| `.github/workflows/ci.yml` | CI | `ubuntu-latest` + `windows-latest` | Windows **App.Tests** |
| `.github/workflows/build-release-exe.yml` | Build release EXE | `windows-latest` | `build-release.ps1`; robocopy → `$LASTEXITCODE = 0` |
| `.github/workflows/release.yml` | Release | tags | Manual |

## Common pitfalls (this repo)

- **Intent router tests**: `skills/manifest.yaml` `intent_map` keys (e.g. `general_chat`, not `general_ai`).
- **WebUiBridge JSON**: `WhenWritingNull`; no `"warning": null` in success JSON.
- **Windows-only** App.Tests — Ubuntu green ≠ done.
- **dotnet --filter**: separate test steps on Windows CI; no `&` in one shell line.

## Optional: local pre-push

```bash
dotnet test ArrayMicRefreshment.CI.slnf -c Release
dotnet test tests/ArrayMicRefreshment.App.Tests/ArrayMicRefreshment.App.Tests.csproj -c Release  # Windows
```
'@
    if (Test-Path $skillPath) {
        $base = Get-Content $skillPath -Raw -Encoding UTF8
        if ($base -notmatch '## Workflows in this repo') {
            $front = @'
---
name: github-actions-ci
description: Post-push GitHub Actions triage (gh run watch, log-failed, Windows jobs). Use when CI is red, after git push, or /github-actions-ci.
---

'@
            if ($base -notmatch '^---') { $base = $front + $base }
            Set-Content -Path $skillPath -Value ($base.TrimEnd() + $amrTail) -Encoding UTF8 -NoNewline
            Write-Host '  ✔ Merged AMR tail into github-actions-ci skill' -ForegroundColor Green
        }
    }
}

Write-Host ''
Write-Host "Done. Project: $ProjectRoot" -ForegroundColor Cyan
Write-Host 'Next: git add .cursor/ && commit && push && gh run watch (post-push-ci-green.mdc)' -ForegroundColor Gray
