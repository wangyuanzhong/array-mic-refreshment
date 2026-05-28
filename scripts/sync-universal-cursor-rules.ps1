[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path,

    [string]$UniversalRepo = '',

    [switch]$WhatIf
)

# sync-universal-cursor-rules.ps1 — refresh .cursor/rules from cursor-universal-rule
#
#   .\scripts\sync-universal-cursor-rules.ps1
#   .\scripts\sync-universal-cursor-rules.ps1 -UniversalRepo C:\src\cursor-universal-rule

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path $ProjectRoot).Path

if (-not $UniversalRepo) {
    $UniversalRepo = Join-Path $env:TEMP 'cursor-universal-rule'
    if (-not (Test-Path (Join-Path $UniversalRepo 'rules'))) {
        Write-Host "→ Cloning cursor-universal-rule to $UniversalRepo ..." -ForegroundColor Cyan
        if (-not $WhatIf) {
            if (Test-Path $UniversalRepo) { Remove-Item $UniversalRepo -Recurse -Force }
            git clone --depth 1 https://github.com/wangyuanzhong/cursor-universal-rule.git $UniversalRepo
        }
    }
}

$installer = Join-Path $UniversalRepo 'scripts\install-universal-rules.ps1'
if (-not (Test-Path $installer)) {
    throw "install-universal-rules.ps1 not found under $UniversalRepo"
}

Write-Host "→ Installing universal rules into $ProjectRoot" -ForegroundColor Cyan
& $installer -ProjectRoot $ProjectRoot -UniversalRepo $UniversalRepo -FixGitignore @(
    $(if ($WhatIf) { '-WhatIf' })
)

Write-Host ""
Write-Host "Note: Re-apply project overlays if needed:" -ForegroundColor Yellow
Write-Host "  - .cursor/skills/frontend-design/ (project-only)" -ForegroundColor Gray
Write-Host "  - AMR-specific lines in .cursor/skills/github-actions-ci/SKILL.md" -ForegroundColor Gray
Write-Host "  - This repo may keep merged 00-universal-core / exe-packaging sections — diff after sync." -ForegroundColor Gray
