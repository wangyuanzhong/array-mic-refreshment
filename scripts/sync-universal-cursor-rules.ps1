[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path,

    [string]$UniversalRepo = '',

    [switch]$Refresh,

    [switch]$WhatIf
)

# sync-universal-cursor-rules.ps1 — refresh .cursor/rules from cursor-universal-rule (rules-only, 0.9.0+)
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

$srcRules = Join-Path $UniversalRepo 'rules'
if (-not (Test-Path $srcRules)) {
    throw "rules/ not found under $UniversalRepo — expected cursor-universal-rule 0.9.0+ layout"
}

$destRules = Join-Path $ProjectRoot '.cursor\rules'
Write-Host "→ Copying rules/*.mdc into $destRules" -ForegroundColor Cyan
if (-not $WhatIf) {
    New-Item -ItemType Directory -Path $destRules -Force | Out-Null
    Copy-Item (Join-Path $srcRules '*.mdc') $destRules -Force
}

if (-not $WhatIf) {
    $sha = (git -C $UniversalRepo rev-parse HEAD).Trim()
    $lockPath = Join-Path $ProjectRoot '.cursor\UNIVERSAL_RULE_LOCK'
    @(
        "cursor-universal-rule=$sha"
        "universal-pack-version=0.9.1"
        "synced=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK')"
        "url=https://github.com/wangyuanzhong/cursor-universal-rule"
    ) | Set-Content -Path $lockPath -Encoding UTF8
    Write-Host "  ✔ .cursor/UNIVERSAL_RULE_LOCK ($sha)" -ForegroundColor Green

    $overlays = Join-Path $PSScriptRoot 'apply-amr-cursor-overlays.ps1'
    & $overlays -ProjectRoot $ProjectRoot

    # 0.9.0: github-actions-ci skill removed upstream — drop stale copy if present
    $staleSkill = Join-Path $ProjectRoot '.cursor\skills\github-actions-ci'
    if (Test-Path $staleSkill) {
        Remove-Item $staleSkill -Recurse -Force
        Write-Host '  ✔ Removed .cursor/skills/github-actions-ci (inlined in post-push-ci-green.mdc)' -ForegroundColor Green
    }
}

Write-Host ''
Write-Host "Done. Project: $ProjectRoot" -ForegroundColor Cyan
Write-Host 'Next: git add .cursor/ scripts/ && commit && push && gh run watch (post-push-ci-green.mdc)' -ForegroundColor Gray
