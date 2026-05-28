# GitHub Actions CI — playbook

Policy: [`.cursor/rules/post-push-ci-green.mdc`](../../rules/post-push-ci-green.mdc) and [`.cursor/rules/00-universal-core.mdc`](../../rules/00-universal-core.mdc).

Universal base: [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule). This file adds **array-mic-refreshment** specifics.

## Post-push checklist

1. Branch and SHA: `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`
2. List runs: `gh run list --branch "$(git rev-parse --abbrev-ref HEAD)" --limit 10`
3. Watch: `gh run watch --exit-status`
4. Failed logs: `gh run view <run-id> --log-failed` — last 80–150 lines, then first `FAIL` / `##[error]`
5. Fix → commit → push → repeat until triggered runs are green
6. Report: run IDs, root cause, files changed

## Workflows in this repo

| Workflow file | Name | Runner | Notes |
|---------------|------|--------|-------|
| `.github/workflows/ci.yml` | CI | `ubuntu-latest` + `windows-latest` | Windows runs **App.Tests** — most regressions here |
| `.github/workflows/build-release-exe.yml` | Build release EXE | `windows-latest` | `build-release.ps1`; reset `$LASTEXITCODE` after robocopy |
| `.github/workflows/release.yml` | Release | tags | Tag / manual |

## Common pitfalls (this repo)

- **Intent router tests**: use upstream keys from `skills/manifest.yaml` → `router.intent_map` (e.g. `general_chat`, not `general_ai`).
- **WebUiBridge JSON**: null properties omitted (`WhenWritingNull`); tests should not expect `"warning": null`.
- **Windows-only**: `ArrayMicRefreshment.App.Tests` — Ubuntu green ≠ full CI pass.
- **dotnet --filter on Windows CI**: separate `dotnet test` steps per project — do not use `&` in one shell line.
- **robocopy exit 1** after successful copy: `$global:LASTEXITCODE = 0` in `scripts/build-release.ps1`.

## Doc conflict guard

Before a CI-only fix, read `README.md`, `docs/**`, `AGENTS.md`. If the fix contradicts documented UI or features, update docs in the same commit or fix code to match the doc.

## Optional: local pre-push

```bash
dotnet test ArrayMicRefreshment.CI.slnf -c Release
# Windows dev machine:
dotnet test tests/ArrayMicRefreshment.App.Tests/ArrayMicRefreshment.App.Tests.csproj -c Release
```

## Stop conditions

Same as `post-push-ci-green.mdc`: all green; infra blocked; 2 identical failures; cannot satisfy Windows jobs in this environment.
