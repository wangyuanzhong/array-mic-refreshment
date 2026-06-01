using Microsoft.Web.WebView2.WinForms;

namespace ArrayMicRefreshment.App.Web;

/// <summary>
/// High-DPI for embedded WebView2: scale the WinForms host in logical pixels and keep
/// <see cref="WebView2.ZoomFactor"/> at 1 — zoom shrinks <c>innerWidth</c> while fixed-width
/// nav columns stay in CSS px and squeeze the settings content pane.
/// </summary>
internal static class WebViewDpiScaling
{
    public const float BaselineDpi = 96f;

    public static float GetScale(Control control) =>
        control.DeviceDpi / BaselineDpi;

    public static void ApplyToWebView(WebView2 webView, Control host)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        // ZoomFactor shrinks layout (innerWidth) while nav columns stay fixed px — keep 1.0.
        // Host ClientSize is already scaled via ScaleLogicalSize; PerMonitorV2 handles crisp text.
        webView.ZoomFactor = 1.0;
    }

    public static Size ScaleLogicalSize(int logicalWidth, int logicalHeight, float scale)
    {
        var w = (int)Math.Round(logicalWidth * scale);
        var h = (int)Math.Round(logicalHeight * scale);
        return new Size(Math.Max(1, w), Math.Max(1, h));
    }

    public static Size ToLogicalSize(Size clientSize, float scale)
    {
        if (scale <= 0f)
        {
            scale = 1f;
        }

        return new Size(
            Math.Max(1, (int)Math.Round(clientSize.Width / scale)),
            Math.Max(1, (int)Math.Round(clientSize.Height / scale)));
    }
}
