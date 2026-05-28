[CmdletBinding()]
param(
    [switch]$Uninstall
)

# scripts/install-git-hooks.ps1 — optional: rebuild exe after each git commit.
#
#   .\scripts\install-git-hooks.ps1
#   .\scripts\install-git-hooks.ps1 -Uninstall

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$hooksDir = Join-Path $repoRoot '.githooks'

if ($Uninstall) {
    git config --unset core.hooksPath 2>$null
    Write-Host 'Removed core.hooksPath (repo-local hooks disabled).' -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $hooksDir)) {
    throw "Missing $hooksDir"
}

git config core.hooksPath .githooks
Write-Host "Installed git hooks from .githooks (core.hooksPath=.githooks)." -ForegroundColor Green
Write-Host 'post-commit will run: scripts/watch-build-release.ps1 -Once' -ForegroundColor Cyan
Write-Host 'For rebuild on every save (not only commit), run: .\scripts\watch-build-release.ps1' -ForegroundColor Cyan
