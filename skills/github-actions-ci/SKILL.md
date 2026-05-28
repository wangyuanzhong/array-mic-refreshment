# GitHub Actions CI — post-push monitor & fix loop

Use this skill **after every `git push`** (or before declaring a task done) when working in this repository. Goal: **all workflows on the pushed branch are green**; if red, diagnose from logs, fix, commit, push, and re-check until green.

## When to trigger

- You pushed commits to `main` or an open PR branch.
- User reports "CI red" / "Actions failed".
- You changed code under paths watched by workflows (`src/`, `ui/`, `tests/`, `scripts/`, `.github/workflows/`).

## Workflows in this repo

| Workflow file | Name | Runner | Notes |
|---------------|------|--------|-------|
| `.github/workflows/ci.yml` | CI | `ubuntu-latest` + `windows-latest` | Windows runs **App.Tests** — most regressions appear here |
| `.github/workflows/build-release-exe.yml` | Build release EXE | `windows-latest` | `build-release.ps1`; robocopy exit 1 is success |
| `.github/workflows/release.yml` | Release | tags | Manual / tag driven |

## Post-push checklist (agent)

1. **Identify branch and SHA**
   ```bash
   git rev-parse --abbrev-ref HEAD
   git rev-parse HEAD
   ```

2. **List recent runs** (needs `gh` auth in environment)
   ```bash
   gh run list --branch "$(git rev-parse --abbrev-ref HEAD)" --limit 5
   ```

3. **Wait for in-progress runs** (up to ~15 min for Windows jobs)
   ```bash
   gh run watch --exit-status
   ```
   Or poll: `gh run list --branch … --json status,conclusion -q '.[0]'`

4. **On failure — fetch logs for failed jobs only**
   ```bash
   gh run view <run-id> --log-failed
   ```
   Read the **last 80–150 lines** first; scroll up for the first `FAIL` / `##[error]`.

5. **Map failure → fix**
   - **App.Tests** on Windows: run the same project locally if possible; fix test data to match `skills/manifest.yaml` `intent_map` keys (e.g. `general_chat`, not `general_ai`).
   - **robocopy / PowerShell exit 1** after successful copy: reset `$global:LASTEXITCODE = 0` after robocopy in `scripts/build-release.ps1`.
   - **npm / ui build**: `cd ui && npm ci && npm run build`.
   - **dotnet**: `dotnet test tests/ArrayMicRefreshment.App.Tests/... -c Release`.

6. **Fix, commit, push**, then repeat from step 2 until **all** relevant workflows for that push are `success`.

7. **Report to user**: which run IDs failed, root cause, files changed, and confirmation that latest runs are green.

## Common pitfalls (this repo)

- **Intent router tests** must use upstream intent keys from `skills/manifest.yaml` → `router.intent_map` (e.g. `write_code`, `general_chat`), not internal specialist ids like `general_ai`.
- **Bridge JSON** omits null properties (`DefaultIgnoreCondition.WhenWritingNull` on `WebUiBridge`); tests must use `TryGetProperty` only when the property should exist, or assert on value not key presence for optional fields.
- **Windows-only tests**: Linux agents cannot run `ArrayMicRefreshment.App.Tests`; do not assume green Ubuntu job means full CI pass.
- **Shell `&` in dotnet `--filter`**: CI uses separate `dotnet test` steps per project on Windows — do not combine with `&` in one shell line.

## Optional: local pre-push

```bash
dotnet test ArrayMicRefreshment.CI.slnf -c Release
# On Windows dev machine:
dotnet test tests/ArrayMicRefreshment.App.Tests/ArrayMicRefreshment.App.Tests.csproj -c Release
```

## Definition of done

- Latest push on the branch has **CI** and **Build release EXE** (if path filters triggered) with `conclusion: success`.
- No known failing tests left unfixed on Windows.
