#Requires -Version 7.0
<#
.SYNOPSIS
  定时轮询并展示并行开发 agent 的分支 / PR / CI 状态。

.DESCRIPTION
  适用于并行开发场景：你把两个（或更多）分支名传给脚本，脚本会按固定间隔输出：
  - 分支最新提交（SHA / message / 时间）
  - 相对 base 分支的 ahead/behind
  - 变更文件数
  - PR 链接、草稿状态、检查状态（可选，依赖 gh）

  常见用法（轮询两位 agent）：
    pwsh -File .\scripts\monitor-agent-status.ps1 `
      -Branches cursor/agent-a-91a6,cursor/agent-b-91a6 `
      -IntervalSeconds 30 `
      -StopWhenChecksGreen
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Branches,

    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$BaseRef = "origin/main",

    [ValidateRange(1, 3600)]
    [int]$IntervalSeconds = 30,

    [ValidateRange(0, 1000000)]
    [int]$MaxIterations = 0,

    [switch]$SkipPrChecks,

    [switch]$StopWhenChecksGreen
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args
    )

    $output = & git @Args 2>&1
    $code = $LASTEXITCODE
    [pscustomobject]@{
        ExitCode = $code
        Output   = ($output -join "`n").Trim()
    }
}

function Get-BranchMetrics {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BranchName,
        [Parameter(Mandatory = $true)]
        [string]$BaseReference
    )

    $remoteRef = "origin/$BranchName"

    # Fetch single branch to reduce network overhead.
    $fetch = Invoke-Git -Args @("fetch", "origin", $BranchName)
    if ($fetch.ExitCode -ne 0) {
        return [pscustomobject]@{
            Branch         = $BranchName
            Reachable      = $false
            Error          = "fetch failed: $($fetch.Output)"
            ShaShort       = "-"
            CommitTime     = "-"
            CommitSubject  = "-"
            Ahead          = "-"
            Behind         = "-"
            ChangedFiles   = "-"
        }
    }

    $sha = Invoke-Git -Args @("rev-parse", "--short", $remoteRef)
    $time = Invoke-Git -Args @("log", "-1", "--format=%cd", "--date=iso-strict", $remoteRef)
    $subject = Invoke-Git -Args @("log", "-1", "--format=%s", $remoteRef)
    $lr = Invoke-Git -Args @("rev-list", "--left-right", "--count", "$BaseReference...$remoteRef")
    $diffCount = Invoke-Git -Args @("diff", "--name-only", "$BaseReference...$remoteRef")

    $behind = "-"
    $ahead = "-"
    if ($lr.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($lr.Output)) {
        $parts = $lr.Output -split "\s+"
        if ($parts.Length -ge 2) {
            $behind = $parts[0]
            $ahead = $parts[1]
        }
    }

    $changedFiles = if ($diffCount.ExitCode -eq 0) {
        if ([string]::IsNullOrWhiteSpace($diffCount.Output)) { 0 } else { ($diffCount.Output -split "`n").Count }
    } else {
        "-"
    }

    [pscustomobject]@{
        Branch        = $BranchName
        Reachable     = $true
        Error         = ""
        ShaShort      = if ($sha.ExitCode -eq 0) { $sha.Output } else { "-" }
        CommitTime    = if ($time.ExitCode -eq 0) { $time.Output } else { "-" }
        CommitSubject = if ($subject.ExitCode -eq 0) { $subject.Output } else { "-" }
        Ahead         = $ahead
        Behind        = $behind
        ChangedFiles  = $changedFiles
    }
}

function Get-PrMetrics {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BranchName
    )

    if ($SkipPrChecks) {
        return [pscustomobject]@{
            PrState      = "skipped"
            PrUrl        = "-"
            CheckSummary = "skipped"
            ChecksGreen  = $false
        }
    }

    $hasGh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $hasGh) {
        return [pscustomobject]@{
            PrState      = "gh-missing"
            PrUrl        = "-"
            CheckSummary = "gh missing"
            ChecksGreen  = $false
        }
    }

    $json = & gh pr list --head $BranchName --limit 1 --json state,isDraft,url,statusCheckRollup 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return [pscustomobject]@{
            PrState      = "query-failed"
            PrUrl        = "-"
            CheckSummary = "pr query failed"
            ChecksGreen  = $false
        }
    }

    $prs = $json | ConvertFrom-Json
    if (-not $prs -or $prs.Count -eq 0) {
        return [pscustomobject]@{
            PrState      = "no-pr"
            PrUrl        = "-"
            CheckSummary = "no pr"
            ChecksGreen  = $false
        }
    }

    $pr = $prs[0]
    $checks = @($pr.statusCheckRollup)
    if (-not $checks -or $checks.Count -eq 0) {
        return [pscustomobject]@{
            PrState      = if ($pr.isDraft) { "draft" } else { $pr.state.ToLowerInvariant() }
            PrUrl        = $pr.url
            CheckSummary = "no checks"
            ChecksGreen  = $false
        }
    }

    $states = $checks | ForEach-Object {
        if ($null -ne $_.conclusion -and $_.conclusion -ne "") { $_.conclusion }
        elseif ($null -ne $_.state -and $_.state -ne "") { $_.state }
        else { "UNKNOWN" }
    }

    $group = $states | Group-Object | Sort-Object Name
    $summary = ($group | ForEach-Object { "$($_.Name):$($_.Count)" }) -join ", "

    $checksGreen =
        ($states.Count -gt 0) -and
        ($states | Where-Object { $_ -notin @("SUCCESS", "NEUTRAL", "SKIPPED") }).Count -eq 0

    [pscustomobject]@{
        PrState      = if ($pr.isDraft) { "draft" } else { $pr.state.ToLowerInvariant() }
        PrUrl        = $pr.url
        CheckSummary = $summary
        ChecksGreen  = $checksGreen
    }
}

if (-not (Test-Path -Path $RepoRoot)) {
    throw "RepoRoot does not exist: $RepoRoot"
}

$uniqueBranches = $Branches | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() } | Select-Object -Unique
if (-not $uniqueBranches -or $uniqueBranches.Count -eq 0) {
    throw "At least one non-empty branch name is required."
}

Push-Location $RepoRoot
try {
    $repoCheck = Invoke-Git -Args @("rev-parse", "--is-inside-work-tree")
    if ($repoCheck.ExitCode -ne 0 -or $repoCheck.Output -ne "true") {
        throw "RepoRoot is not a git repository: $RepoRoot"
    }

    $baseCheck = Invoke-Git -Args @("rev-parse", "--verify", $BaseRef)
    if ($baseCheck.ExitCode -ne 0) {
        throw "BaseRef not found: $BaseRef. Try 'git fetch origin main' first."
    }

    $iteration = 0
    $startTime = Get-Date

    while ($true) {
        $iteration++
        $now = Get-Date
        $elapsed = New-TimeSpan -Start $startTime -End $now

        $rows = foreach ($branch in $uniqueBranches) {
            $branchMetrics = Get-BranchMetrics -BranchName $branch -BaseReference $BaseRef
            $prMetrics = Get-PrMetrics -BranchName $branch
            [pscustomobject]@{
                Branch       = $branchMetrics.Branch
                SHA          = $branchMetrics.ShaShort
                Ahead        = $branchMetrics.Ahead
                Behind       = $branchMetrics.Behind
                Files        = $branchMetrics.ChangedFiles
                PR           = $prMetrics.PrState
                Checks       = $prMetrics.CheckSummary
                CommitTime   = $branchMetrics.CommitTime
                Commit       = $branchMetrics.CommitSubject
                PrUrl        = $prMetrics.PrUrl
                Reachable    = $branchMetrics.Reachable
                ChecksGreen  = $prMetrics.ChecksGreen
                Error        = $branchMetrics.Error
            }
        }

        Write-Host ""
        Write-Host ("=" * 120)
        Write-Host ("[{0}] iteration={1} elapsed={2:hh\:mm\:ss} base={3}" -f $now.ToString("yyyy-MM-dd HH:mm:ss"), $iteration, $elapsed, $BaseRef)
        Write-Host ("=" * 120)
        $rows |
            Select-Object Branch, SHA, Ahead, Behind, Files, PR, Checks, CommitTime, Commit |
            Format-Table -AutoSize

        foreach ($row in $rows) {
            if (-not [string]::IsNullOrWhiteSpace($row.PrUrl) -and $row.PrUrl -ne "-") {
                Write-Host ("  {0} -> {1}" -f $row.Branch, $row.PrUrl)
            }
            if (-not [string]::IsNullOrWhiteSpace($row.Error)) {
                Write-Warning ("{0}: {1}" -f $row.Branch, $row.Error)
            }
        }

        if ($StopWhenChecksGreen) {
            $allGreen = ($rows | Where-Object { $_.Reachable -eq $true }).Count -eq $rows.Count -and
                        ($rows | Where-Object { $_.PR -in @("open", "merged", "closed", "draft") -and $_.ChecksGreen -eq $true }).Count -eq $rows.Count
            if ($allGreen) {
                Write-Host "All monitored branches have green checks. Stopping monitor."
                break
            }
        }

        if ($MaxIterations -gt 0 -and $iteration -ge $MaxIterations) {
            break
        }

        Start-Sleep -Seconds $IntervalSeconds
    }
}
finally {
    Pop-Location
}
