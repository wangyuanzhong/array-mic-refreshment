namespace ArrayMicRefreshment.App;

/// <summary>
/// Macaron design tokens shared with <c>ui/src/styles/tokens.css</c>.
/// Keep in sync when CSS HUD / accent values change.
/// </summary>
internal static class DesignTokens
{
    // Light frosted HUD (matches ui/hud.html)
    public static readonly Color HudBackground = Color.FromArgb(250, 250, 252);

    public static readonly Color HudText = Color.FromArgb(61, 61, 86);

    public static readonly Color HudError = Color.FromArgb(217, 74, 106);

    public static readonly Color HudAccent = Color.FromArgb(42, 159, 212);

    public static readonly Color HudWake = Color.FromArgb(123, 95, 199);

    public static readonly Font HudFont = new("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point);

    public const float HudOpacity = 0.97f;
}
