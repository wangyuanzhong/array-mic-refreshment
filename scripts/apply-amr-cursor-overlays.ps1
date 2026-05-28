[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path
)

# apply-amr-cursor-overlays.ps1 — project-specific patches after universal rule sync

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$rules = Join-Path $ProjectRoot '.cursor\rules'

$core = @'
---
description: Universal agent workflow — definition of done, doc/code priority, local vs cloud
alwaysApply: true
---

# Universal core (all projects using this rules pack)

Synced from [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule). Lock: see `.cursor/UNIVERSAL_RULE_LOCK`.

## Definition of done

A task is **not finished** until **all** of the following that apply are satisfied:

1. **Code/build** matches user intent and passes relevant tests you can run in this environment.
2. **Documentation**: every project `.md` and `.txt` is reviewed and updated if this change affects behavior, UI, CLI, config, or ops (see `docs-sync-before-finish.mdc`).
3. **EXE packaging** (if this repo is an exe/desktop packager): local build or CI exe job per `exe-packaging-local-cloud.mdc`.
4. **Git**: if you commit/push, `.cursor/` is tracked per `git-track-cursor-folder.mdc`.
5. **Post-push CI** (cloud / after push): per `post-push-ci-green.mdc` — **no opt-out** (`.cursor/.local-skip-post-push-ci` is ignored).

## Local vs cloud (one rules pack, two behaviors)

Detect mode **once per task**:

| Mode | When |
|------|------|
| **Cloud** | Cursor Cloud Agent; or Linux/remote agent without the project's native desktop toolchain; or env `CI=true` on a push-monitoring task |
| **Local** | Cursor Desktop on the developer machine with repo checkout |

Apply the **Cloud** or **Local** subsection in each rule file. Do **not** maintain separate rule files per environment.

**Post-push CI**: always run `post-push-ci-green.mdc` after `git push` (including Local mode). Do not document or rely on `.cursor/.local-skip-post-push-ci`.

## Conflict priority (fixes, CI, refactors)

When code, tests, CI, and docs disagree, resolve in this order:

1. **Product truth** in project docs: root `README.md`, `docs/**/*.md`, `AGENTS.md`, `CHANGELOG.md`, and any `.txt` spec shipped with the app — **functionality and UI definitions in these files win** over ad-hoc agent guesses.
2. **This conversation** and explicit user messages in the current task.
3. **Code** (implementation) — update code *or* update docs deliberately; never silently diverge.
4. **CI/workflow YAML** — must reflect (1) and (3).

If a CI fix would violate (1), **change the test or implementation** or **update the doc in the same PR** with a short note — do not "make CI green" by breaking documented behavior.

## Honesty

Rules do not run shell commands by themselves. **You (Agent) must execute** build, test, `gh`, and doc edits. Report blockers instead of claiming done.

**No exceptions for "small" changes** — if you `git push`, you still run post-push CI monitoring when the rule applies; if you changed behavior or agent docs, you still run docs sync. Skipping because the diff is only `.cursor/` or "just a fix" is a rule violation.

## This repo (array-mic-refreshment)

Also read [`AGENTS.md`](../../AGENTS.md) (任务收尾自检) and [`docs/LOCAL_DEVELOPMENT.md`](../../docs/LOCAL_DEVELOPMENT.md). Project-only Agent skills: `.cursor/skills/frontend-design/` (`/frontend-design`).
'@

$exe = @'
---
description: Desktop/EXE repos — local release build; cloud CI must include exe packaging workflow
alwaysApply: true
---

# EXE packaging — local build & cloud CI

Synced from [cursor-universal-rule](https://github.com/wangyuanzhong/cursor-universal-rule). Lock: `.cursor/UNIVERSAL_RULE_LOCK`.

## Detect "EXE packaging repo"

Any of these at repo root:

- `scripts/watch-build-release.ps1`
- `scripts/build-release.ps1`
- `scripts/build_exe.ps1`
- `scripts/pack-ready.ps1`
- `scripts/ci_and_build.ps1`
- Or `.csproj` / docs stating a shipped desktop `.exe`

**This repo matches** (`watch-build-release.ps1`, `build-release.ps1`).

---

## Local (Cursor Desktop)

When **Local** mode (see `00-universal-core.mdc`) and this task changed **app/UI/packaging** (`src/`, `ui/`, `wwwroot`, packaging scripts):

1. **Run one release build** before marking done (unless user said "docs only / no build"):

   ```powershell
   .\scripts\watch-build-release.ps1 -Once
   ```

   Full offline folder (models in dist):

   ```powershell
   .\scripts\watch-build-release.ps1 -Once -IncludeModels
   ```

   Fallback: `.\scripts\build-release.ps1 -Mode self-contained`

2. If `dist/` is locked, stop `ArrayMicRefreshment` then retry.
3. Report `dist\ArrayMicRefreshment-self-contained\ArrayMicRefreshment.exe` **full path** and **LastWriteTime**.
4. Do **not** skip because the task was "only git merge" if `src/`/`ui/` changed on disk.

Optional (remind user once): `.\scripts\install-git-hooks.ps1`, background `.\scripts\watch-build-release.ps1`.

Log: `dist\watch-build.log`.

---

## Cloud (Cursor Cloud Agent / Linux)

Cannot run Windows desktop `.exe` locally. You **must**:

1. **Verify CI** includes `.github/workflows/build-release-exe.yml` on `windows-latest` running `build-release.ps1`.
2. If missing, add a minimal workflow (path filters on `src/`, `ui/`, `scripts/build-release.ps1`, solution file).
3. After **push**, ensure that workflow is among **triggered** runs and reaches **success** (see `post-push-ci-green.mdc`).
4. State in the handoff: "Local exe not built here; verified via CI run &lt;id&gt;."

---

## Consistency with docs

Exe output, WebView2 `wwwroot`, and `models/` layout must match `README.md` / `docs/**` — update docs in the same task if packaging layout changes (`docs-sync-before-finish.mdc`).
'@

Set-Content -Path (Join-Path $rules '00-universal-core.mdc') -Value $core.TrimEnd() -Encoding UTF8 -NoNewline
Set-Content -Path (Join-Path $rules 'exe-packaging-local-cloud.mdc') -Value $exe.TrimEnd() -Encoding UTF8 -NoNewline

Write-Host '✔ Applied AMR overlays (00-universal-core, exe-packaging)' -ForegroundColor Green
