[CmdletBinding()]
param(
    [ValidateSet('framework-dep', 'self-contained')]
    [string]$Mode = 'framework-dep',

    [string]$OutputDir = 'dist',

    [switch]$Zip,

    [switch]$IncludeModels
)

# scripts/build-release.ps1 — produce a runnable Array Mic Refreshment build.
#
# Usage examples:
#   .\scripts\build-release.ps1
#       → dist\ArrayMicRefreshment-framework-dep\ (needs .NET 8 Desktop Runtime on target)
#
#   .\scripts\build-release.ps1 -Mode self-contained
#       → dist\ArrayMicRefreshment-self-contained\ (no runtime needed on target, ~150 MB)
#
#   .\scripts\build-release.ps1 -Mode self-contained -IncludeModels -Zip
#       → bundles models/ + skills/ into output; also dist\ArrayMicRefreshment-self-contained.zip
#
#   Full offline zip (~2.7 GB) is typically named ArrayMicRefreshment-ready.zip
#   (self-contained + all ASR/speaker models). See README.md and CHANGELOG.md.

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$proj = 'src\ArrayMicRefreshment.App\ArrayMicRefreshment.App.csproj'
$outDir = Join-Path $OutputDir "ArrayMicRefreshment-$Mode"
$uiDir = Join-Path $repoRoot 'ui'
$wwwSrc = Join-Path $repoRoot 'src\ArrayMicRefreshment.App\wwwroot'

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

# Route B: build Web UI before dotnet publish (docs/UI_ROUTE_B_WEBVIEW2.md §9.2)
if (-not (Test-Path (Join-Path $uiDir 'package.json'))) {
    throw "ui/package.json not found — Web UI sources are required for release builds."
}

Write-Host "→ Building Web UI (npm ci && npm run build) ..." -ForegroundColor Cyan
Push-Location $uiDir
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed (exit $LASTEXITCODE)" }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

if (-not (Test-Path (Join-Path $wwwSrc 'index.html'))) {
    throw "Web UI build did not produce $wwwSrc\index.html"
}

Write-Host "✔ Web UI built → $wwwSrc" -ForegroundColor Green

$selfContained = ($Mode -eq 'self-contained')
$publishArgs = @(
    'publish', $proj,
    '-c', 'Release',
    '-r', 'win-x64',
    "--self-contained:$($selfContained.ToString().ToLower())",
    '-p:PublishSingleFile=false',     # WinForms apps don't bundle native deps well into a single file
    '-p:DebugType=embedded',
    '-o', $outDir
)

Write-Host "→ dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# Ensure wwwroot is present in publish output (csproj CopyToOutputDirectory + explicit sync)
$wwwDst = Join-Path $outDir 'wwwroot'
Write-Host "→ Copying wwwroot to $wwwDst ..." -ForegroundColor Cyan
if (Test-Path $wwwDst) { Remove-Item $wwwDst -Recurse -Force }
robocopy $wwwSrc $wwwDst /E /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy wwwroot failed (exit $LASTEXITCODE)" }
Write-Host "✔ Bundled wwwroot into release folder" -ForegroundColor Green

$exe = Join-Path $outDir 'ArrayMicRefreshment.exe'
if (-not (Test-Path $exe)) { throw "Expected $exe to exist after publish" }

$size = (Get-Item $exe).Length / 1MB
Write-Host ""
Write-Host "✔ Built $exe ($([math]::Round($size,2)) MB)" -ForegroundColor Green
Write-Host "  Output dir: $outDir" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
if ($Mode -eq 'framework-dep') {
    Write-Host "  1. On the target machine install .NET 8 Desktop Runtime:" -ForegroundColor Yellow
    Write-Host "     winget install Microsoft.DotNet.DesktopRuntime.8" -ForegroundColor Yellow
}
Write-Host "  2. Download ASR + speaker models (once):" -ForegroundColor Yellow
Write-Host "     cd <repo root>; .\\scripts\\download-models.ps1" -ForegroundColor Yellow
Write-Host "     (or place the model under <unzipped folder>\\models\\ manually)" -ForegroundColor Yellow
Write-Host "  3. Install WebView2 Runtime if settings/enrollment Web UI fails to open:" -ForegroundColor Yellow
Write-Host "     winget install Microsoft.EdgeWebView2Runtime" -ForegroundColor Yellow
Write-Host "  4. Double-click ArrayMicRefreshment.exe" -ForegroundColor Yellow

$skillsSrc = Join-Path $repoRoot 'skills'
$skillsDst = Join-Path $outDir 'skills'
if (Test-Path $skillsSrc) {
    Write-Host "→ Copying skills to $skillsDst ..." -ForegroundColor Cyan
    if (Test-Path $skillsDst) { Remove-Item $skillsDst -Recurse -Force }
    robocopy $skillsSrc $skillsDst /E /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy skills failed (exit $LASTEXITCODE)" }
    Write-Host "✔ Bundled skills into release folder" -ForegroundColor Green
}

$modelsSrc = Join-Path $repoRoot 'models'
$modelsDst = Join-Path $outDir 'models'
if ($IncludeModels -and (Test-Path $modelsSrc)) {
    Write-Host "→ Copying models to $modelsDst ..." -ForegroundColor Cyan
    if (Test-Path $modelsDst) { Remove-Item $modelsDst -Recurse -Force }
    robocopy $modelsSrc $modelsDst /E /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy models failed (exit $LASTEXITCODE)" }
    Write-Host "✔ Bundled models into release folder" -ForegroundColor Green
}

if ($Zip) {
    $zipPath = "$outDir.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host ""
    Write-Host "✔ Zipped → $zipPath ($([math]::Round($zipSize,2)) MB)" -ForegroundColor Green
}
