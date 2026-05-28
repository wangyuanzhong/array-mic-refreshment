# Cursor Agent skills

Place **agent-only** `SKILL.md` trees here so Desktop and Cloud share the same instructions.

Each skill folder needs `SKILL.md` with YAML frontmatter:

- `name` — must match the folder name (e.g. `frontend-design`)
- `description` — when the agent should use it

**Slash invoke:** `/frontend-design` (hyphen, not a space). Or `@frontend-design` in chat.

If `/` does not list it: `git pull`, use Cursor 2.4+, check **Settings → Rules → Agent Decides**.

Runtime LLM skills for the app remain in the repo root [`skills/`](../skills/) (`manifest.yaml`, `upstream/`).
