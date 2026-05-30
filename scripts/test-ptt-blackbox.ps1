[CmdletBinding()]
param(
    [string]$Configuration = 'Release',

    [string]$InitialHotkey = 'Ctrl+Alt+Space',

    [string]$ChangedHotkey = 'Ctrl+Shift+F9'
)

# test-ptt-blackbox.ps1 — PTT 自动化（键盘钩子 + 模拟按键）
#
# 覆盖：
#   1) 钩子层黑盒（任意解析成功的热键表达式）
#   2) E2E：启动托盘同款栈 → Web 设置保存改热键 → 按新热键 → 验证开录
#
# Usage (repo root, Windows PowerShell):
#   .\scripts\test-ptt-blackbox.ps1
#   .\scripts\test-ptt-blackbox.ps1 -ChangedHotkey 'Ctrl+Shift+F10'
#
# 模拟你在设置里改成的热键（不必是 Space）：
#   $env:AMR_PTT_E2E_INITIAL = 'Ctrl+Alt+F8'
#   $env:AMR_PTT_E2E_CHANGED = 'Ctrl+Win+F9'
#   .\scripts\test-ptt-blackbox.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

if ($env:OS -ne 'Windows_NT') {
    Write-Host 'N/A: PTT tests require Windows (SendInput + WH_KEYBOARD_LL).' -ForegroundColor Yellow
    exit 0
}

$env:AMR_PTT_E2E_INITIAL = $InitialHotkey
$env:AMR_PTT_E2E_CHANGED = $ChangedHotkey

Write-Host '=== PTT automated tests ===' -ForegroundColor Cyan
Write-Host "E2E flow: start app stack -> save hotkey '$InitialHotkey' -> '$ChangedHotkey' -> press -> recording" -ForegroundColor Gray
Write-Host ''

$integrationProj = 'tests\ArrayMicRefreshment.Integration.Tests\ArrayMicRefreshment.Integration.Tests.csproj'

Write-Host "-> dotnet test (Category=PttBlackBox|PttE2e)" -ForegroundColor Cyan
dotnet test $integrationProj -c $Configuration --filter 'Category=PttBlackBox|Category=PttE2e' --verbosity normal
if ($LASTEXITCODE -ne 0) {
    throw 'PTT automated tests failed'
}

Write-Host ''
Write-Host 'OK PTT tests passed (hook + settings-save E2E).' -ForegroundColor Green
