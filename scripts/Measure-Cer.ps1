#Requires -Version 5.1
<#
.SYNOPSIS
  Measure SenseVoice CER on scripts/cer-test-set.json and write docs/CER_BASELINE.md.
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$cerProject = Join-Path $PSScriptRoot "CerMeasure\CerMeasure.csproj"
Push-Location $RepoRoot
try {
    & $dotnet run --project $cerProject -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "CerMeasure failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$baseline = Join-Path $RepoRoot "docs\CER_BASELINE.md"
if (-not (Test-Path $baseline)) {
    throw "Expected $baseline after CerMeasure run."
}

Write-Host "CER baseline written to $baseline"
