#!/usr/bin/env bash
# Refresh skills/upstream/ from third-party repos (verbatim).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UP="$ROOT/skills/upstream"

mkdir -p "$UP/danielrosehill/text-transform" "$UP/florianbruniaux" "$UP/majiayu000" "$UP/shanttoosh"

curl -fsSL -o "$UP/danielrosehill/voice-prompt-enhancement-node.prompt.md" \
  "https://raw.githubusercontent.com/danielrosehill/Voice-Prompt-Enhancement-Node/main/prompt.md"

curl -fsSL -o "$UP/danielrosehill/stt-basic-cleanup.complete-system-prompt.md" \
  "https://raw.githubusercontent.com/danielrosehill/STT-Basic-Cleanup-System-Prompt/main/complete-system-prompt.md"

curl -fsSL -o "$UP/danielrosehill/text-transform/code-editing.md" \
  "https://raw.githubusercontent.com/danielrosehill/Text-Transformation-Prompt-Collection-2/main/by-use-case/ai/development/code-editing.md"

curl -fsSL -o "$UP/danielrosehill/text-transform/general-prompt.md" \
  "https://raw.githubusercontent.com/danielrosehill/Text-Transformation-Prompt-Collection-2/main/by-use-case/ai/general-prompt.md"

curl -fsSL -o "$UP/danielrosehill/text-transform/deep-research-prompt.md" \
  "https://raw.githubusercontent.com/danielrosehill/Text-Transformation-Prompt-Collection-2/main/by-use-case/ai/deep-research-prompt.md"

curl -fsSL -o "$UP/danielrosehill/text-transform/to-do-list.md" \
  "https://raw.githubusercontent.com/danielrosehill/Text-Transformation-Prompt-Collection-2/main/by-use-case/to-do-list.md"

curl -fsSL -o "$UP/florianbruniaux/voice-refine.SKILL.md" \
  "https://raw.githubusercontent.com/FlorianBruniaux/claude-code-ultimate-guide/main/examples/skills/voice-refine/SKILL.md"

curl -fsSL -o "$UP/majiayu000/dictation-githubnext-gh-aw-2.SKILL.md" \
  "https://raw.githubusercontent.com/majiayu000/claude-skill-registry/main/skills/other/dictation-githubnext-gh-aw-2/SKILL.md"

echo "Synced upstream prompts into $UP"
echo "Note: shanttoosh intent prompt is extracted from intent.py — re-copy manually if upstream changes."
