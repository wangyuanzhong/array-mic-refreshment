namespace ArrayMicRefreshment.App;

/// <summary>
/// Macaron design tokens shared with <c>ui/src/styles/tokens.css</c>.
/// Keep in sync when CSS HUD / accent values change.
/// </summary>
internal static class DesignTokens
{
    // --hud-bg: rgb(45 40 58 / 92%) — Form.BackColor must be opaque; use HudOpacity on the form.
    public static readonly Color HudBackground = Color.FromArgb(45, 40, 58);

    // --hud-text: #faf8ff
    public static readonly Color HudText = Color.FromArgb(250, 248, 255);

    // --hud-error: #ffb8d0
    public static readonly Color HudError = Color.FromArgb(255, 184, 208);

    // --hud-accent: #7ec8e3
    public static readonly Color HudAccent = Color.FromArgb(126, 200, 227);

    // --font-sans / --font-size-sm (13px ≈ 9.75pt)
    public static readonly Font HudFont = new("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);

    public const float HudOpacity = 0.92f;
}
