#!/usr/bin/env bash
# Polls every 120s for the 3 expected branches & their CI status.
# Writes a status snapshot to /tmp/merge-watch.log on every tick.
set -uo pipefail
BRANCHES=(
  "cursor/audio-ptt-vad-ead1"
  "cursor/sherpa-asr-speaker-ead1"
  "cursor/skill-pipeline-output-ead1"
)
LOG=/tmp/merge-watch.log
cd /workspace
echo "watch started at $(date -u +%FT%TZ)" >| "$LOG"
TICK=0
while true; do
  TICK=$((TICK+1))
  TS=$(date -u +%FT%TZ)
  {
    echo "===== tick #$TICK @ $TS ====="
    git fetch --all --prune --quiet 2>&1 | tail -5
    READY_COUNT=0
    for B in "${BRANCHES[@]}"; do
      if git show-ref --verify --quiet "refs/remotes/origin/$B"; then
        SHA=$(git rev-parse --short "origin/$B")
        PR_JSON=$(gh pr list --head "$B" --state all --json number,state,statusCheckRollup,headRefOid,title --limit 1 2>/dev/null || echo "[]")
        if [ "$PR_JSON" = "[]" ] || [ -z "$PR_JSON" ]; then
          echo "[$B] branch@$SHA  PR=NONE"
        else
          STATE=$(echo "$PR_JSON" | jq -r '.[0].state')
          NUM=$(echo "$PR_JSON" | jq -r '.[0].number')
          # statusCheckRollup is an array of checks; reduce to summary
          ROLLUP=$(echo "$PR_JSON" | jq -r '
            .[0].statusCheckRollup // []
            | if length == 0 then "no-checks"
              else
                ( map(.status // .state // "UNKNOWN") | unique | join(",") )
                + " / " +
                ( map(.conclusion // .state // "PENDING") | unique | join(",") )
              end')
          echo "[$B] branch@$SHA  PR#$NUM state=$STATE  checks=$ROLLUP"
          # Mark ready if all checks succeeded
          ALL_OK=$(echo "$PR_JSON" | jq -r '
            .[0].statusCheckRollup // []
            | if length == 0 then "NO_CHECKS"
              else
                ( all(.; (.conclusion // .state) as $c |
                    ($c == "SUCCESS" or $c == "NEUTRAL" or $c == "SKIPPED")) )
                | tostring
              end')
          if [ "$ALL_OK" = "true" ]; then
            READY_COUNT=$((READY_COUNT+1))
          fi
        fi
      else
        echo "[$B] not pushed yet"
      fi
    done
    echo "ready=$READY_COUNT/${#BRANCHES[@]}"
  } >> "$LOG" 2>&1
  sleep 120
done
