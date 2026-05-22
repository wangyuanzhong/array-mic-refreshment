#!/usr/bin/env bash
set -uo pipefail
BRANCHES=(
  "cursor/winbuild-ci-fix-ead1"
  "cursor/settings-ui-completion-ead1"
  "cursor/integration-e2e-tests-ead1"
)
LOG=/tmp/merge-watch2.log
cd /workspace
echo "watch round 2 started at $(date -u +%FT%TZ)" >| "$LOG"
TICK=0
while true; do
  TICK=$((TICK+1))
  TS=$(date -u +%FT%TZ)
  {
    echo "===== tick #$TICK @ $TS ====="
    git fetch --all --prune --quiet 2>&1 | tail -3
    for B in "${BRANCHES[@]}"; do
      if git show-ref --verify --quiet "refs/remotes/origin/$B"; then
        SHA=$(git rev-parse --short "origin/$B")
        PR_JSON=$(gh pr list --head "$B" --state all --json number,state,statusCheckRollup,headRefOid,mergeable,mergeStateStatus --limit 1 2>/dev/null || echo "[]")
        if [ "$PR_JSON" = "[]" ] || [ -z "$PR_JSON" ]; then
          echo "[$B] branch@$SHA  PR=NONE"
        else
          STATE=$(echo "$PR_JSON" | jq -r '.[0].state')
          NUM=$(echo "$PR_JSON" | jq -r '.[0].number')
          MERGE=$(echo "$PR_JSON" | jq -r '.[0].mergeStateStatus // "?"')
          ROLLUP=$(echo "$PR_JSON" | jq -r '
            .[0].statusCheckRollup // []
            | if length == 0 then "no-checks"
              else (map(.conclusion // .state // "PENDING") | unique | join(",")) end')
          echo "[$B] branch@$SHA  PR#$NUM state=$STATE merge=$MERGE  checks=$ROLLUP"
        fi
      else
        echo "[$B] not pushed yet"
      fi
    done
  } >> "$LOG" 2>&1
  sleep 120
done
