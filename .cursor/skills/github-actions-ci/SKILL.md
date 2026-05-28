---
name: github-actions-ci
description: Post-push GitHub Actions triage (gh run watch, log-failed, Windows jobs). Use when CI is red, after git push, or /github-actions-ci.
---

# GitHub Actions CI — playbook (generic)

Policy: [`.cursor/rules/post-push-ci-green.mdc`](../../rules/post-push-ci-green.mdc).

Use this skill for commands and triage after `git push`. Do **not** treat this file as optional when the rule applies.

## Post-push checklist

1. Branch and SHA: `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`
2. List runs: `gh run list --branch "$(git rev-parse --abbrev-ref HEAD)" --limit 10`
3. Watch: `gh run watch --exit-status` (or watch each in-progress id)
4. Failed logs: `gh run view <run-id> --log-failed` — read last 80 lines, then first `FAIL` / `##[error]`
5. Fix → commit → push → repeat until triggered runs are green
6. Report: run IDs, root cause, files changed

## Discover workflows in *this* repo

```bash
ls .github/workflows/
```

Read each YAML for: `on.push` paths, job names, `runs-on` (especially `windows-latest` for desktop tests / exe build).

## EXE packaging repos

Expect at least:

- A **CI** workflow with tests (often split per project on Windows)
- A **build-release-exe** (or similar) on `windows-latest` calling `scripts/build-release.ps1`

If push changed `src/` or `ui/` but exe workflow did not run, check path filters.

## Common fix patterns

| Symptom | Things to try |
|---------|----------------|
| Test name / assertion drift | Align test with `README` / `docs` / manifest; update doc if behavior intentionally changed |
| `robocopy` exit code 1 on success | Reset `$global:LASTEXITCODE = 0` after robocopy in PowerShell build scripts |
| npm / frontend | `cd ui && npm ci && npm run build` |
| dotnet | Match CI's `dotnet test` project paths locally on Windows when possible |
| Workflow syntax | Validate `on`, `paths`, job `needs` |

## Doc conflict guard

Before merging a "CI-only" fix, grep or read relevant `docs/` and root `README.md`. If the fix contradicts documented UI or features, update docs in the same commit or fix the code to match the doc.

## Stop conditions

Same table as `post-push-ci-green.mdc`: all green; infra blocked; 2 identical failures; cannot satisfy Windows jobs in this environment.

## Workflows in this repo (array-mic-refreshment)

| Workflow file | Name | Runner | Notes |
|---------------|------|--------|-------|
| `.github/workflows/ci.yml` | CI | `ubuntu-latest` + `windows-latest` | Windows **App.Tests** |
| `.github/workflows/build-release-exe.yml` | Build release EXE | `windows-latest` | `build-release.ps1`; robocopy → `$LASTEXITCODE = 0` |
| `.github/workflows/release.yml` | Release | tags | Manual |

## Common pitfalls (this repo)

- **Intent router tests**: `skills/manifest.yaml` `intent_map` keys (e.g. `general_chat`, not `general_ai`).
- **WebUiBridge JSON**: `WhenWritingNull`; no `"warning": null` in success JSON.
- **Windows-only** App.Tests — Ubuntu green ≠ done.
- **dotnet --filter**: separate test steps on Windows CI; no `&` in one shell line.
- **Job stuck on App.Tests for hours**: usually `vstest` never exits (leaked `GlobalHotkeyListener` / WinForms `Timer` from undisposed `NAudioPushToTalkSource`). Fix tests to use stub PTT; CI uses `--blame-hang-timeout` and `timeout-minutes` on the job.

## Optional: local pre-push

```bash
dotnet test ArrayMicRefreshment.CI.slnf -c Release
dotnet test tests/ArrayMicRefreshment.App.Tests/ArrayMicRefreshment.App.Tests.csproj -c Release  # Windows
```
