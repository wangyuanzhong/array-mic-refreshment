# Frontend design — Array Mic Refreshment Web UI

Use this skill when styling or restructuring the **Route B** WebView2 UI under `ui/`.

## Product constraints

- **Do not** change C# audio, ASR, wake, or pipeline behavior — CSS/TS presentation only unless explicitly wiring bridge calls.
- Keep bundle **light** (vanilla TS, no React unless approved).
- All colors and radii must come from `ui/src/styles/tokens.css` (macaron system below).
- Match WinForms **field parity**; do not remove settings sections.

## Macaron design system (required palette)

Soft, low-contrast pastels — readable, friendly, not neon.

| Token role | Light mode | Usage |
|------------|------------|--------|
| Page background | `#FFF8FB` → gradient to `#F3FAFF` | Full-page wash |
| Surface (cards, nav) | `#FFFFFF` / `#FFFCFE` | Cards, sidebar |
| Text primary | `#3D3D56` | Body, labels |
| Text muted | `#7A7A9A` | Hints, subtitles |
| Border | `#F0E4F0` | Cards, inputs |
| Accent (primary) | `#7EC8E3` sky macaron | Primary buttons, active nav |
| Accent hover | `#5BB5D6` | Button hover |
| Accent soft | `#E8F6FC` | Nav active bg, focus rings |
| Secondary | `#FFB8D0` blush | Badges, highlights |
| Tertiary | `#C9B6F0` lavender | Section accents |
| Mint | `#B8E8D0` | Success states |
| Butter | `#FFE9B8` | Warnings, mock banner |
| Danger | `#F5A8BC` | Errors |

### Layout

- Left nav **240px**, content scrolls, **sticky footer** for Save/Cancel on settings.
- Settings: inner section nav + stacked `card` groups.
- Generous whitespace (`--space-4` / `--space-6` between cards).

### Components

- **Cards**: white surface, `--radius-md`, soft `--shadow-sm`, optional 1px top accent gradient (lavender → blush).
- **Primary button**: sky macaron fill, white text, slight shadow on hover (no layout shift).
- **Ghost button**: transparent, border `--color-border`.
- **Inputs**: full width, `--radius-sm`, focus ring `2px` `--color-accent-soft` + border `--color-accent`.
- **Active nav**: `--color-accent-soft` background, `--color-accent` text — not saturated blue slabs.

### Typography

- `Segoe UI`, system-ui stack (already in tokens).
- Title 16px semibold; body 14px; hints 13px muted.

### Dark mode

Optional `prefers-color-scheme: dark`: deepen bg to `#1A1625`, surfaces `#252033`, keep pastel accents slightly desaturated.

## Files to edit

| File | Purpose |
|------|---------|
| `ui/src/styles/tokens.css` | Single source of truth |
| `ui/src/styles/components.css` | Shared components |
| `ui/src/pages/*.ts` | Page structure only; classes from components.css |
| `docs/UI_ROUTE_B_WEBVIEW2.md` §5 | Sync token table when palette changes |

## Verification

```bash
cd ui && npm run build
```

Visual check in WebView2 on Windows after `dotnet build` App project.
