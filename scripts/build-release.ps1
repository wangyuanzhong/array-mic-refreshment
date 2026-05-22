[CmdletBinding()]
param(
    [ValidateSet('framework-dep', 'self-contained')]
    [string]$Mode = 'framework-dep',

    [string]$OutputDir = 'dist',

    [switch]$Zip
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
#   .\scripts\build-release.ps1 -Mode self-contained -Zip
#       → also produces dist\ArrayMicRefreshment-self-contained.zip

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$proj = 'src\ArrayMicRefreshment.App\ArrayMicRefreshment.App.csproj'
$outDir = Join-Path $OutputDir "ArrayMicRefreshment-$Mode"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

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
Write-Host "  2. Download the SenseVoice model (once):" -ForegroundColor Yellow
Write-Host "     cd <unzipped folder>; ..\\scripts\\download-models.ps1" -ForegroundColor Yellow
Write-Host "     (or place the model under <unzipped folder>\\models\\ manually)" -ForegroundColor Yellow
Write-Host "  3. Double-click ArrayMicRefreshment.exe" -ForegroundColor Yellow

if ($Zip) {
    $zipPath = "$outDir.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host ""
    Write-Host "✔ Zipped → $zipPath ($([math]::Round($zipSize,2)) MB)" -ForegroundColor Green
}
