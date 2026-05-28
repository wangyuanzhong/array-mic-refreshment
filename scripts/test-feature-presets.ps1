# Feature preset + Route B regression (Windows for App.Tests)
param(
    [switch]$Full
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "→ Core: FeaturePreset tests" -ForegroundColor Cyan
dotnet test tests/ArrayMicRefreshment.Core.Tests/ArrayMicRefreshment.Core.Tests.csproj -c Release `
    --filter "FullyQualifiedName~FeaturePreset" --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($IsWindows -or $env:OS -match 'Windows') {
    Write-Host "→ UI build" -ForegroundColor Cyan
    Push-Location ui
    if (-not (Test-Path node_modules)) { npm ci }
    npm run build
    Pop-Location

    Write-Host "→ App.Tests: FeaturePreset + Phase2" -ForegroundColor Cyan
    dotnet test tests/ArrayMicRefreshment.App.Tests/ArrayMicRefreshment.App.Tests.csproj -c Release `
        --filter "FullyQualifiedName~FeaturePreset|Phase=RouteB2"
    exit $LASTEXITCODE
}

Write-Host "✔ Linux/macOS: Core FeaturePreset tests passed (App.Tests require Windows)." -ForegroundColor Green
exit 0
