[CmdletBinding()]
param(
    [ValidateSet('framework-dep', 'self-contained')]
    [string]$Mode = 'self-contained',

    [switch]$IncludeModels,

    [switch]$Once,

    [int]$DebounceSeconds = 12,

    [int]$PollSeconds = 3
)

# scripts/watch-build-release.ps1
#
# Watches src/, ui/, and build scripts; after changes settle, runs build-release.ps1.
#
# Usage:
#   .\scripts\watch-build-release.ps1
#       → keep running; rebuild exe when you save files (default: self-contained, no models copy)
#
#   .\scripts\watch-build-release.ps1 -Once
#       → single build then exit (git hook / Agent)
#
#   .\scripts\watch-build-release.ps1 -IncludeModels
#       → also robocopy models/ into dist (slow; ~2 GB)
#
# Stop with Ctrl+C. Log: dist\watch-build.log

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$logPath = Join-Path $repoRoot 'dist\watch-build.log'
$lockPath = Join-Path $repoRoot 'dist\.watch-build.lock'
$stampPath = Join-Path $repoRoot 'dist\.watch-build-last-success.txt'

function Write-Log([string]$Message, [ConsoleColor]$Color = [ConsoleColor]::Gray) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    $dir = Split-Path $logPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Add-Content -Path $logPath -Value $line -Encoding UTF8
    Write-Host $line -ForegroundColor $Color
}

function Test-ShouldWatchFile([string]$FullPath) {
    $rel = $FullPath.Substring($repoRoot.Path.Length).TrimStart('\', '/').Replace('\', '/')
    if ($rel -match '^(dist|bin|obj|\.git|models|node_modules|\.cursor|agent-tools|agent-transcripts)(/|$)') {
        return $false
    }
    if ($rel -match '/(bin|obj)/') { return $false }
    if ($rel -match '\.(user|suo|cache)$') { return $false }
    return $true
}

function Get-WatchRoots {
    @(
        (Join-Path $repoRoot 'src'),
        (Join-Path $repoRoot 'ui'),
        (Join-Path $repoRoot 'scripts'),
        (Join-Path $repoRoot 'ArrayMicRefreshment.sln'),
        (Join-Path $repoRoot 'VERSION.txt')
    ) | Where-Object { Test-Path $_ }
}

function Get-LatestSourceWriteUtc {
    $latest = [datetime]::MinValue
    foreach ($root in (Get-WatchRoots)) {
        if (Test-Path $root -PathType Leaf) {
            $t = (Get-Item $root).LastWriteTimeUtc
            if ($t -gt $latest) { $latest = $t }
            continue
        }

        Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { Test-ShouldWatchFile $_.FullName } |
            ForEach-Object {
                if ($_.LastWriteTimeUtc -gt $latest) {
                    $latest = $_.LastWriteTimeUtc
                }
            }
    }
    return $latest
}

function Stop-RunningApp {
    $proc = Get-Process -Name 'ArrayMicRefreshment' -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Log 'Stopping running ArrayMicRefreshment.exe so dist/ can be updated.' Yellow
        $proc | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

function Invoke-ReleaseBuild {
    if (Test-Path $lockPath) {
        $lockAge = (Get-Date) - (Get-Item $lockPath).LastWriteTime
        if ($lockAge.TotalMinutes -lt 30) {
            Write-Log 'Build already in progress (lock file present). Skipping.' DarkYellow
            return $false
        }
        Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType File -Path $lockPath -Force | Out-Null
    try {
        Stop-RunningApp
        Write-Log "-> build-release.ps1 -Mode $Mode$(if ($IncludeModels) { ' -IncludeModels' })" Cyan
        $buildScript = Join-Path $repoRoot 'scripts\build-release.ps1'
        if ($IncludeModels) {
            & $buildScript -Mode $Mode -IncludeModels
        }
        else {
            & $buildScript -Mode $Mode
        }
        if ($LASTEXITCODE -ne 0) { throw "build-release.ps1 failed (exit $LASTEXITCODE)" }

        $exe = Join-Path $repoRoot "dist\ArrayMicRefreshment-$Mode\ArrayMicRefreshment.exe"
        if (-not (Test-Path $exe)) { throw "Expected output missing: $exe" }

        Set-Content -Path $stampPath -Value (Get-Date).ToString('o') -Encoding UTF8
        Write-Log "OK $exe" Green
        return $true
    }
    catch {
        Write-Log "FAIL Build failed: $($_.Exception.Message)" Red
        return $false
    }
    finally {
        if (Test-Path $lockPath) { Remove-Item $lockPath -Force -ErrorAction SilentlyContinue }
    }
}

if ($Once) {
    $ok = Invoke-ReleaseBuild
    exit $(if ($ok) { 0 } else { 1 })
}

Write-Log 'Watch build started. Press Ctrl+C to stop.' Green
Write-Log "Repo: $repoRoot" Gray
Write-Log "Debounce: ${DebounceSeconds}s | Poll: ${PollSeconds}s | Mode: $Mode" Gray
Write-Log "Log file: $logPath" Gray

$lastBuiltUtc = if (Test-Path $stampPath) {
    [datetime]::Parse((Get-Content $stampPath -Raw).Trim()).ToUniversalTime()
} else {
    [datetime]::MinValue
}

$pendingLatestUtc = $null

try {
    while ($true) {
        $latest = Get-LatestSourceWriteUtc
        if ($latest -gt $lastBuiltUtc) {
            if (-not $pendingLatestUtc -or $latest -gt $pendingLatestUtc) {
                $pendingLatestUtc = $latest
                Write-Log 'Source changed - waiting for edits to settle...' DarkCyan
            }

            $quietCutoff = (Get-Date).ToUniversalTime().AddSeconds(-$DebounceSeconds)
            if ($pendingLatestUtc -le $quietCutoff) {
                if (Invoke-ReleaseBuild) {
                    $lastBuiltUtc = Get-LatestSourceWriteUtc
                }
                $pendingLatestUtc = $null
            }
        }
        else {
            $pendingLatestUtc = $null
        }

        Start-Sleep -Seconds $PollSeconds
    }
}
finally {
    Write-Log 'Watch build stopped.' Yellow
}
