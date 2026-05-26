#Requires -Version 5.1
<#
.SYNOPSIS
  Download Sherpa-ONNX SenseVoice models listed in ModelManifest.json.

.DESCRIPTION
  Run from repository root on Windows (PowerShell 5.1+):
    .\scripts\download-models.ps1
    .\scripts\download-models.ps1 -ModelsRoot D:\models -Package asr-primary
    .\scripts\download-models.ps1 -IncludeSpeaker

  Extracts archives under models/ by default (gitignored).
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ModelsRoot = "",
    [ValidateSet("asr-primary", "asr-fallback", "all")]
    [string]$Package = "asr-primary",
    [switch]$IncludeSpeaker,
    [switch]$SkipSpeaker,
    [switch]$IncludeKws,
    [switch]$SkipKws
)

$ErrorActionPreference = "Stop"

if (-not $PSBoundParameters.ContainsKey('IncludeSpeaker') -and -not $SkipSpeaker) {
    $IncludeSpeaker = $true
}
if ($SkipSpeaker) {
    $IncludeSpeaker = $false
}

if (-not $PSBoundParameters.ContainsKey('IncludeKws') -and -not $SkipKws) {
    $IncludeKws = $false
}
if ($SkipKws) {
    $IncludeKws = $false
}

if ([string]::IsNullOrWhiteSpace($ModelsRoot)) {
    $ModelsRoot = Join-Path $RepoRoot "models"
}

$manifestPath = Join-Path $PSScriptRoot "ModelManifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "ModelManifest.json not found at $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
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

function Download-PackageArchive {
    param($pkg, [string]$UrlBase)

    $destExtract = Join-Path $ModelsRoot $pkg.extractDir
    if (Test-Path $destExtract) {
        Write-Host "[skip] $($pkg.id) already exists at $destExtract"
        return
    }

    $cacheDir = Join-Path $ModelsRoot ".cache"
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $archivePath = Join-Path $cacheDir $pkg.archive
    $url = "$UrlBase/$($pkg.archive)"

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

function Download-SpeakerOnnx {
    param($pkg, [string]$UrlBase)

    $destDir = Join-Path $ModelsRoot $pkg.extractDir
    $destFile = Join-Path $destDir $pkg.file
    if (Test-Path $destFile) {
        Write-Host "[skip] $($pkg.id) already exists at $destFile"
        return
    }

    $cacheDir = Join-Path $ModelsRoot ".cache"
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $cacheFile = Join-Path $cacheDir $pkg.file
    $url = "$UrlBase/$($pkg.file)"

    Write-Host "[download] $url"
    Invoke-WebRequest -Uri $url -OutFile $cacheFile -UseBasicParsing

    if ($pkg.sha256) {
        $hash = (Get-FileHash -Algorithm SHA256 -Path $cacheFile).Hash.ToLowerInvariant()
        if ($hash -ne $pkg.sha256.ToLowerInvariant()) {
            throw "SHA256 mismatch for $($pkg.id): expected $($pkg.sha256), got $hash"
        }
    }

    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item -Path $cacheFile -Destination $destFile -Force
    Write-Host "[done] $($pkg.id) -> $destFile"
}

foreach ($pkg in $packages) {
    Download-PackageArchive -pkg $pkg -UrlBase $baseUrl
}

if ($IncludeSpeaker) {
    $speakerBase = $manifest.phase2Speaker.baseUrl.TrimEnd("/")
    foreach ($pkg in @($manifest.phase2Speaker.packages)) {
        Download-SpeakerOnnx -pkg $pkg -UrlBase $speakerBase
    }
}

if ($IncludeKws -and $manifest.phase2Kws) {
    $kwsBase = $manifest.phase2Kws.baseUrl.TrimEnd("/")
    foreach ($pkg in @($manifest.phase2Kws.packages)) {
        Download-PackageArchive -pkg $pkg -UrlBase $kwsBase
    }
}

Write-Host ""
Write-Host "Models root: $ModelsRoot"
Write-Host "Point AppSettings.ModelsDirectory or Sherpa config to the extracted folder."
if (-not $IncludeSpeaker) {
    Write-Host "Speaker model: omitted. Re-run without -SkipSpeaker (default downloads speaker ONNX)."
}
if (-not $IncludeKws) {
    Write-Host "KWS model: omitted. Re-run with -IncludeKws for wake-word detection."
}
