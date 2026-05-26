# pack-ready.ps1 — clean dist, build offline package, single zip (Explorer-safe).
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

Stop-Process -Name 'ArrayMicRefreshment' -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$dist = Join-Path $repoRoot 'dist'
Write-Host '-> Cleaning dist...' -ForegroundColor Cyan
if (-not (Test-Path $dist)) {
    New-Item -ItemType Directory -Path $dist | Out-Null
}
else {
    Get-ChildItem $dist -Force | Remove-Item -Recurse -Force -ErrorAction Stop
}

Write-Host '-> Build self-contained + models...' -ForegroundColor Cyan
& (Join-Path $repoRoot 'scripts\build-release.ps1') -Mode self-contained -IncludeModels
$builtExe = Join-Path $repoRoot 'dist\ArrayMicRefreshment-self-contained\ArrayMicRefreshment.exe'
if (-not (Test-Path $builtExe)) { throw "build-release failed: missing $builtExe" }

if (Get-Command sherpa-onnx-cli -ErrorAction SilentlyContinue) {
    Write-Host '-> Refresh wake phrase encodings...' -ForegroundColor Cyan
    & (Join-Path $repoRoot 'scripts\generate-wake-encodings.ps1')
}

$outDir = Join-Path $dist 'ArrayMicRefreshment'
$src = Join-Path $dist 'ArrayMicRefreshment-self-contained'
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
Rename-Item $src $outDir

$readmePath = Join-Path $PSScriptRoot 'pack-readme.txt'
if (Test-Path $readmePath) {
    Copy-Item $readmePath (Join-Path $outDir 'README.txt') -Force
    Copy-Item $readmePath (Join-Path $dist 'README.txt') -Force
}

$zipPath = Join-Path $dist 'ArrayMicRefreshment.zip'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host '-> Compress-Archive single zip (~3 GB, several minutes)...' -ForegroundColor Cyan
$items = Get-ChildItem $outDir -Force
Compress-Archive -Path ($items | ForEach-Object { $_.FullName }) -DestinationPath $zipPath -CompressionLevel Optimal -Force

$mb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ''
Write-Host "OK folder: $outDir" -ForegroundColor Green
Write-Host "OK zip:    $zipPath ($mb MB)" -ForegroundColor Green
