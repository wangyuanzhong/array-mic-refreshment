[CmdletBinding()]
param(
    [switch]$Restore
)

# scripts/cursor-local-opt-out-post-push-ci.ps1
#
# Cloud / main 保留 .cursor/rules/post-push-ci-green.mdc（push 后等 Actions 全绿）。
# 本机若不想启用该 rule（且不写入全局 User Rules），运行本脚本一次即可。
#
#   .\scripts\cursor-local-opt-out-post-push-ci.ps1          # 本机禁用
#   .\scripts\cursor-local-opt-out-post-push-ci.ps1 -Restore # 恢复仓库版本

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ruleRel = '.cursor/rules/post-push-ci-green.mdc'
$rulePath = Join-Path $repoRoot $ruleRel

Set-Location $repoRoot

if ($Restore) {
    git update-index --no-skip-worktree $ruleRel 2>$null
    git checkout -- $ruleRel
    if (Test-Path $rulePath) {
        Write-Host "✔ Restored $ruleRel (post-push CI rule active locally again)." -ForegroundColor Green
    }
    else {
        Write-Host "✔ skip-worktree cleared; file missing — run git checkout from origin/main if needed." -ForegroundColor Yellow
    }
    exit 0
}

if (-not (git rev-parse --verify HEAD:$ruleRel 2>$null)) {
    Write-Warning "Rule not in current branch: $ruleRel"
    exit 0
}

git update-index --skip-worktree $ruleRel
if (Test-Path $rulePath) {
    Remove-Item -LiteralPath $rulePath -Force
}
Write-Host "✔ Local opt-out: $ruleRel hidden (skip-worktree)." -ForegroundColor Green
Write-Host '  Git 历史中仍有该文件；push 不会删除云端 rule。' -ForegroundColor Cyan
Write-Host '  Cloud Agent / 其他 clone 仍按 main 上的 rule 执行。' -ForegroundColor Gray
Write-Host "  恢复: .\scripts\cursor-local-opt-out-post-push-ci.ps1 -Restore" -ForegroundColor Gray
