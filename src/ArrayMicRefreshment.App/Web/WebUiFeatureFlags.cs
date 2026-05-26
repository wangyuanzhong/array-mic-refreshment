namespace ArrayMicRefreshment.App.Web;

/// <summary>Phase 1 feature toggles for WebView2 settings shell vs legacy WinForms.</summary>
internal static class WebUiFeatureFlags
{
    /// <summary>
    /// When true (default), tray「设置」opens <see cref="WebUiHostForm"/>.
    /// Set environment variable <c>AMR_USE_WINFORMS_SETTINGS=1</c> to use <see cref="SettingsForm"/> instead.
    /// </summary>
    public static bool UseWebSettings { get; } = !IsTruthy(
        Environment.GetEnvironmentVariable("AMR_USE_WINFORMS_SETTINGS"));

    private static bool IsTruthy(string? value) =>
        value is "1" or "true" or "TRUE" or "yes" or "YES";
}
