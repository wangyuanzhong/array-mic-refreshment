[CmdletBinding()]
param(
    [switch]$SkipUiBuild,

    [switch]$Full,

    [string]$Configuration = 'Release'
)

# test-phase2-route-b.ps1 — Route B Phase 2 automated acceptance (Windows)
#
# Covers Bridge/ApplyService parity (docs/UI_ROUTE_B_WEBVIEW2.md §8) via unit tests.
# Does NOT replace §10.2 manual PTT/wake/microphone regression.
#
# Usage (repo root, PowerShell):
#   .\scripts\test-phase2-route-b.ps1
#   .\scripts\test-phase2-route-b.ps1 -Full          # also run ArrayMicRefreshment.CI.slnf
#   dotnet test tests/ArrayMicRefreshment.App.Tests --filter "Phase=RouteB2"

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

function Write-Step([string]$Msg, [ConsoleColor]$Color = 'Cyan') {
    Write-Host $Msg -ForegroundColor $Color
}

Write-Step '=== Route B Phase 2 automated acceptance ===' 'Cyan'
Write-Host 'Maps to docs/UI_ROUTE_B_WEBVIEW2.md §8 (Web settings). §10.2 audio still manual.' -ForegroundColor Gray
Write-Host ''

if (-not $SkipUiBuild) {
    Write-Step '→ Building Web UI (ui/npm run build) ...'
    Push-Location (Join-Path $repoRoot 'ui')
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed ($LASTEXITCODE)" }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed ($LASTEXITCODE)" }
    Pop-Location
    $index = Join-Path $repoRoot 'src\ArrayMicRefreshment.App\wwwroot\index.html'
    if (-not (Test-Path $index)) { throw "Missing $index after npm build" }
    Write-Host "  ✔ wwwroot ready" -ForegroundColor Green
}

Write-Step "→ dotnet build ArrayMicRefreshment.sln -c $Configuration"
dotnet build ArrayMicRefreshment.sln -c $Configuration --no-restore 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet restore ArrayMicRefreshment.sln
    dotnet build ArrayMicRefreshment.sln -c $Configuration
}
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

$appTestProj = 'tests\ArrayMicRefreshment.App.Tests\ArrayMicRefreshment.App.Tests.csproj'

Write-Step '→ Phase 2 acceptance tests (filter Phase=RouteB2) ...'
dotnet test $appTestProj -c $Configuration --no-build --filter 'Phase=RouteB2' --verbosity normal
if ($LASTEXITCODE -ne 0) { throw 'Phase2 RouteB2 tests failed' }

Write-Step '→ All App.Tests (Windows) ...'
dotnet test $appTestProj -c $Configuration --no-build --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'App.Tests failed' }

if ($Full) {
    Write-Step '→ Cross-platform CI solution tests ...'
    dotnet test ArrayMicRefreshment.CI.slnf -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'CI slnf tests failed' }
}

Write-Host ''
Write-Host '✔ Automated Phase 2 checks passed.' -ForegroundColor Green
Write-Host ''
Write-Host 'Still required locally (cannot automate here):' -ForegroundColor Yellow
Write-Host '  • §10.2 — PTT / wake / paste / mic / memory after Web save' -ForegroundColor Gray
Write-Host '  • Visual WebView2 settings UI smoke' -ForegroundColor Gray
Write-Host '  • Optional: AMR_USE_WINFORMS_SETTINGS=1 vs default Web settings diff' -ForegroundColor Gray
Write-Host ''
Write-Host 'Optional release smoke:' -ForegroundColor Yellow
Write-Host '  .\scripts\watch-build-release.ps1 -Once' -ForegroundColor Gray
