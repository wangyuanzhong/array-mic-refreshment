#Requires -Version 5.1
<#
.SYNOPSIS
  Download Sherpa-ONNX SenseVoice models listed in ModelManifest.json.

.DESCRIPTION
  Run from repository root on Windows (PowerShell 5.1+):
    .\scripts\download-models.ps1
    .\scripts\download-models.ps1 -ModelsRoot D:\models -Package asr-primary

  Extracts archives under models/ by default (gitignored).
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ModelsRoot = "",
    [ValidateSet("asr-primary", "asr-fallback", "all")]
    [string]$Package = "asr-primary"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ModelsRoot)) {
    $ModelsRoot = Join-Path $RepoRoot "models"
}

$manifestPath = Join-Path $PSScriptRoot "ModelManifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "ModelManifest.json not found at $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$baseUrl = $manifest.baseUrl.TrimEnd("/")

New-Item -ItemType Directory -Force -Path $ModelsRoot | Out-Null

$packages = @($manifest.packages)
if ($Package -ne "all") {
    $packages = $packages | Where-Object { $_.role -eq $Package }
    if (-not $packages) {
        throw "No package with role '$Package' in manifest."
    }
}

function Expand-ArchiveFile {
    param([string]$ArchivePath, [string]$DestDir)
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
    if ($ArchivePath -match '\.tar\.bz2$') {
        if (-not (Get-Command tar -ErrorAction SilentlyContinue)) {
            throw "tar is required to extract .tar.bz2 (Windows 10 1803+ includes tar)."
        }
        tar -xjf $ArchivePath -C $DestDir
    }
    elseif ($ArchivePath -match '\.zip$') {
        Expand-Archive -Path $ArchivePath -DestinationPath $DestDir -Force
    }
    else {
        throw "Unsupported archive: $ArchivePath"
    }
}

foreach ($pkg in $packages) {
    $url = "$baseUrl/$($pkg.archive)"
    $destExtract = Join-Path $ModelsRoot $pkg.extractDir
    if (Test-Path $destExtract) {
        Write-Host "[skip] $($pkg.id) already exists at $destExtract"
        continue
    }

    $cacheDir = Join-Path $ModelsRoot ".cache"
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $archivePath = Join-Path $cacheDir $pkg.archive

    Write-Host "[download] $url"
    Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing

    if ($pkg.sha256) {
        $hash = (Get-FileHash -Algorithm SHA256 -Path $archivePath).Hash.ToLowerInvariant()
        if ($hash -ne $pkg.sha256.ToLowerInvariant()) {
            throw "SHA256 mismatch for $($pkg.id): expected $($pkg.sha256), got $hash"
        }
    }

    Write-Host "[extract] $($pkg.id) -> $ModelsRoot"
    Expand-ArchiveFile -ArchivePath $archivePath -DestDir $ModelsRoot
    Write-Host "[done] $($pkg.id)"
}

Write-Host ""
Write-Host "Models root: $ModelsRoot"
Write-Host "Point AppSettings.ModelsDirectory or Sherpa config to the extracted folder."
