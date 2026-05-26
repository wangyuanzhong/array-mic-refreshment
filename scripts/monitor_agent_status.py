#!/usr/bin/env python3
"""
Timer-based status monitor for parallel coding agents.

Example:
  python3 scripts/monitor_agent_status.py \
    --branches cursor/agent-a-91a6 cursor/agent-b-91a6 \
    --interval 30 \
    --stop-when-checks-green
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import List, Optional


@dataclass
class BranchMetrics:
    branch: str
    reachable: bool
    error: str
    sha_short: str
    commit_time: str
    commit_subject: str
    ahead: str
    behind: str
    changed_files: str


@dataclass
class PrMetrics:
    pr_state: str
    pr_url: str
    check_summary: str
    checks_green: bool


def run_cmd(cmd: List[str], cwd: Path) -> tuple[int, str]:
    proc = subprocess.run(
        cmd,
        cwd=str(cwd),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
    )
    return proc.returncode, proc.stdout.strip()


def git(cwd: Path, *args: str) -> tuple[int, str]:
    return run_cmd(["git", *args], cwd)


def get_branch_metrics(repo_root: Path, branch: str, base_ref: str) -> BranchMetrics:
    remote_ref = f"origin/{branch}"
    code, out = git(repo_root, "fetch", "origin", branch)
    if code != 0:
        return BranchMetrics(
            branch=branch,
            reachable=False,
            error=f"fetch failed: {out}",
            sha_short="-",
            commit_time="-",
            commit_subject="-",
            ahead="-",
            behind="-",
            changed_files="-",
        )

    def safe_git(*argv: str) -> str:
        c, o = git(repo_root, *argv)
        return o if c == 0 and o else "-"

    sha = safe_git("rev-parse", "--short", remote_ref)
    commit_time = safe_git("log", "-1", "--format=%cd", "--date=iso-strict", remote_ref)
    subject = safe_git("log", "-1", "--format=%s", remote_ref)
    c_lr, out_lr = git(repo_root, "rev-list", "--left-right", "--count", f"{base_ref}...{remote_ref}")

    behind = "-"
    ahead = "-"
    if c_lr == 0 and out_lr:
        parts = out_lr.split()
        if len(parts) >= 2:
            behind, ahead = parts[0], parts[1]

    c_diff, out_diff = git(repo_root, "diff", "--name-only", f"{base_ref}...{remote_ref}")
    if c_diff == 0:
        changed_files = str(0 if not out_diff else len(out_diff.splitlines()))
    else:
        changed_files = "-"

    return BranchMetrics(
        branch=branch,
        reachable=True,
        error="",
        sha_short=sha,
        commit_time=commit_time,
        commit_subject=subject,
        ahead=ahead,
        behind=behind,
        changed_files=changed_files,
    )


def get_pr_metrics(repo_root: Path, branch: str, skip_pr_checks: bool) -> PrMetrics:
    if skip_pr_checks:
        return PrMetrics("skipped", "-", "skipped", False)

    if shutil.which("gh") is None:
        return PrMetrics("gh-missing", "-", "gh missing", False)

    code, out = run_cmd(
        [
            "gh",
            "pr",
            "list",
            "--head",
            branch,
            "--limit",
            "1",
            "--json",
            "state,isDraft,url,statusCheckRollup",
        ],
        repo_root,
    )
    if code != 0 or not out:
        return PrMetrics("query-failed", "-", "pr query failed", False)

    try:
        prs = json.loads(out)
    except json.JSONDecodeError:
        return PrMetrics("query-failed", "-", "invalid pr json", False)

    if not prs:
        return PrMetrics("no-pr", "-", "no pr", False)

    pr = prs[0]
    checks = pr.get("statusCheckRollup") or []
    state = "draft" if pr.get("isDraft") else str(pr.get("state", "unknown")).lower()
    url = str(pr.get("url", "-"))

    if not checks:
        return PrMetrics(state, url, "no checks", False)

    statuses: List[str] = []
    for check in checks:
        if check is None:
            continue
        conclusion = check.get("conclusion")
        check_state = check.get("state")
        if conclusion:
            statuses.append(str(conclusion).upper())
        elif check_state:
            statuses.append(str(check_state).upper())
        else:
            statuses.append("UNKNOWN")

    if not statuses:
        return PrMetrics(state, url, "no checks", False)

    summary_map: dict[str, int] = {}
    for s in statuses:
        summary_map[s] = summary_map.get(s, 0) + 1
    summary = ", ".join(f"{k}:{summary_map[k]}" for k in sorted(summary_map))
    checks_green = all(s in {"SUCCESS", "NEUTRAL", "SKIPPED"} for s in statuses)
    return PrMetrics(state, url, summary, checks_green)


def print_table(rows: List[dict[str, str]]) -> None:
    headers = ["Branch", "SHA", "Ahead", "Behind", "Files", "PR", "Checks", "CommitTime", "Commit"]
    widths: dict[str, int] = {h: len(h) for h in headers}

    for row in rows:
        for h in headers:
            widths[h] = min(80, max(widths[h], len(str(row[h]))))

    def fmt_cell(header: str, value: str) -> str:
        text = value
        if len(text) > widths[header]:
            text = text[: widths[header] - 1] + "…"
        return text.ljust(widths[header])

    sep = " | "
    print(sep.join(h.ljust(widths[h]) for h in headers))
    print("-" * (sum(widths.values()) + len(sep) * (len(headers) - 1)))
    for row in rows:
        print(sep.join(fmt_cell(h, str(row[h])) for h in headers))


def main() -> int:
    parser = argparse.ArgumentParser(description="Monitor branch/PR status for parallel coding agents.")
    parser.add_argument("--branches", nargs="+", required=True, help="Remote branch names to monitor.")
    parser.add_argument("--repo-root", default=str(Path(__file__).resolve().parents[1]), help="Git repository root.")
    parser.add_argument("--base-ref", default="origin/main", help="Base ref for ahead/behind and diff counts.")
    parser.add_argument("--interval", type=int, default=30, help="Polling interval (seconds).")
    parser.add_argument(
        "--iterations",
        type=int,
        default=0,
        help="Max iterations (0 means run forever).",
    )
    parser.add_argument("--skip-pr-checks", action="store_true", help="Skip GitHub PR/check polling.")
    parser.add_argument(
        "--stop-when-checks-green",
        action="store_true",
        help="Stop automatically when all branches are reachable and checks are green.",
    )
    args = parser.parse_args()

    if args.interval <= 0:
        print("error: --interval must be > 0", file=sys.stderr)
        return 2

    repo_root = Path(args.repo_root).resolve()
    if not repo_root.exists():
        print(f"error: repo root does not exist: {repo_root}", file=sys.stderr)
        return 2

    code, out = git(repo_root, "rev-parse", "--is-inside-work-tree")
    if code != 0 or out.strip() != "true":
        print(f"error: not a git repository: {repo_root}", file=sys.stderr)
        return 2

    code, _ = git(repo_root, "rev-parse", "--verify", args.base_ref)
    if code != 0:
        print(f"error: base ref not found: {args.base_ref}", file=sys.stderr)
        return 2

    branches = []
    seen = set()
    for b in args.branches:
        branch = b.strip()
        if branch and branch not in seen:
            branches.append(branch)
            seen.add(branch)

    if not branches:
        print("error: no valid branches after normalization", file=sys.stderr)
        return 2

    start = datetime.now(timezone.utc)
    iteration = 0

    while True:
        iteration += 1
        now = datetime.now(timezone.utc)
        elapsed = now - start

        rows: List[dict[str, str]] = []
        enriched: List[tuple[BranchMetrics, PrMetrics]] = []
        for branch in branches:
            bm = get_branch_metrics(repo_root, branch, args.base_ref)
            pm = get_pr_metrics(repo_root, branch, args.skip_pr_checks)
            enriched.append((bm, pm))
            rows.append(
                {
                    "Branch": bm.branch,
                    "SHA": bm.sha_short,
                    "Ahead": bm.ahead,
                    "Behind": bm.behind,
                    "Files": bm.changed_files,
                    "PR": pm.pr_state,
                    "Checks": pm.check_summary,
                    "CommitTime": bm.commit_time,
                    "Commit": bm.commit_subject,
                }
            )

        print("\n" + "=" * 140)
        print(
            f"[{now.strftime('%Y-%m-%d %H:%M:%S UTC')}] "
            f"iteration={iteration} elapsed={str(elapsed).split('.')[0]} base={args.base_ref}"
        )
        print("=" * 140)
        print_table(rows)

        for bm, pm in enriched:
            if pm.pr_url and pm.pr_url != "-":
                print(f"  {bm.branch} -> {pm.pr_url}")
            if bm.error:
                print(f"  WARN {bm.branch}: {bm.error}")

        if args.stop_when_checks_green:
            all_green = all(
                bm.reachable and pm.pr_state in {"open", "draft", "merged", "closed"} and pm.checks_green
                for bm, pm in enriched
            )
            if all_green:
                print("All monitored branches have green checks. Stopping monitor.")
                break

        if args.iterations > 0 and iteration >= args.iterations:
            break

        time.sleep(args.interval)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
