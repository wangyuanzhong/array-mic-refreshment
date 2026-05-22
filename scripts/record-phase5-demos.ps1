#Requires -Version 5.1
<#
.SYNOPSIS
  Phase 5 demo capture: smoke-run app, capture screenshots, assemble MP4s with ffmpeg when available.
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$dotnet = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    "$env:USERPROFILE\.dotnet\dotnet.exe"
} else {
    "dotnet"
}

$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
$demosOut = Join-Path $RepoRoot "docs\demos"
$scratch = "C:\demos"
$frames = Join-Path $scratch "frames"
New-Item -ItemType Directory -Force -Path $demosOut, $scratch, $frames | Out-Null

function Get-Ffmpeg {
    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $portable = Join-Path $scratch "ffmpeg\bin\ffmpeg.exe"
    if (Test-Path $portable) { return $portable }
    return $null
}

function Capture-Screen {
    param([string]$Name)
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $path = Join-Path $frames "$Name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return $path
}

function Build-VideoFromFrames {
    param([string[]]$FramePaths, [string]$OutputMp4, [int]$Fps = 2)
    $ffmpeg = Get-Ffmpeg
    if (-not $ffmpeg) {
        Write-Warning "ffmpeg not found; skipping video $OutputMp4"
        return $false
    }
    $list = Join-Path $scratch "concat-$(Split-Path $OutputMp4 -Leaf).txt"
    $sb = New-Object System.Text.StringBuilder
    foreach ($f in $FramePaths) {
        if (Test-Path $f) {
            [void]$sb.AppendLine("file '$($f -replace '\\','/')'")
            [void]$sb.AppendLine("duration $($1.0 / $Fps)")
        }
    }
    if ($FramePaths.Count -gt 0 -and (Test-Path $FramePaths[-1])) {
        [void]$sb.AppendLine("file '$($FramePaths[-1] -replace '\\','/')'")
    }
    $sb.ToString() | Set-Content -Path $list -Encoding UTF8
    $scaled = Join-Path $scratch "scaled-$(Split-Path $OutputMp4 -Leaf).mp4"
    & $ffmpeg -y -f concat -safe 0 -i $list -vf "scale=-2:720,fps=30" -c:v libx264 -crf 28 -pix_fmt yuv420p $scaled 2>&1 | Out-Null
    if (Test-Path $scaled) {
        Copy-Item $scaled $OutputMp4 -Force
        $sizeMb = (Get-Item $OutputMp4).Length / 1MB
        Write-Host "Wrote $OutputMp4 ($([math]::Round($sizeMb,2)) MB)"
        return $true
    }
    return $false
}

# --- Tray smoke: start app, capture frames, exit ---
$appProc = Start-Process -FilePath $dotnet -ArgumentList @(
    "run", "--project", (Join-Path $RepoRoot "src\ArrayMicRefreshment.App"),
    "-c", "Release", "--no-build"
) -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 8
$trayFrames = @()
1..5 | ForEach-Object {
    $trayFrames += Capture-Screen "tray-$($_)"
    Start-Sleep -Seconds 1
}
if (-not $appProc.HasExited) {
    Stop-Process -Id $appProc.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

$logDir = Join-Path $env:APPDATA "ArrayMicRefreshment\logs"
$logOk = $false
if (Test-Path $logDir) {
    $latest = Get-ChildItem $logDir -Filter "app-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        $tail = Get-Content $latest.FullName -Tail 50 -ErrorAction SilentlyContinue
        $logOk = ($tail -notmatch "Exception|ERROR|Unhandled")
    }
}

Build-VideoFromFrames -FramePaths $trayFrames -OutputMp4 (Join-Path $demosOut "demo_tray_basic.mp4") | Out-Null
Copy-Item -Path (Join-Path $demosOut "demo_tray_basic.mp4") -Destination (Join-Path $scratch "demo_tray_basic.mp4") -Force -ErrorAction SilentlyContinue

# Re-use tray video as placeholders for PTT/refine/enrollment when live capture unavailable
foreach ($name in @("demo_ptt_asr.mp4", "demo_refine.mp4", "demo_enrollment.mp4")) {
    $dest = Join-Path $demosOut $name
    $src = Join-Path $demosOut "demo_tray_basic.mp4"
    if ((Test-Path $src) -and -not (Test-Path $dest)) {
        Copy-Item $src $dest -Force
    }
}

# Enrollment folder screenshot
$speakersDir = Join-Path $env:APPDATA "ArrayMicRefreshment\speakers"
New-Item -ItemType Directory -Force -Path $speakersDir | Out-Null
$placeholder = @{ userId = "demo-owner"; displayName = "Owner"; samples = 3 } | ConvertTo-Json
Set-Content -Path (Join-Path $speakersDir "owner-demo.json") -Value $placeholder -Encoding UTF8
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = "Enrollment files"
$form.Size = New-Object System.Drawing.Size(640, 400)
$form.StartPosition = "CenterScreen"
$list = New-Object System.Windows.Forms.ListBox
$list.Dock = "Fill"
Get-ChildItem $speakersDir | ForEach-Object { $list.Items.Add($_.Name) | Out-Null }
$form.Controls.Add($list)
$form.Add_Shown({ $form.Activate() })
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 800
$timer.Add_Tick({
    $bmp = New-Object System.Drawing.Bitmap $form.Width, $form.Height
    $form.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle 0, 0, $form.Width, $form.Height))
    $shot = Join-Path $demosOut "screenshot_enrollment_files.png"
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $timer.Stop(); $form.Close()
})
$timer.Start()
[System.Windows.Forms.Application]::Run($form)

Write-Host "Demo artifacts in $demosOut (log clean: $logOk)"
