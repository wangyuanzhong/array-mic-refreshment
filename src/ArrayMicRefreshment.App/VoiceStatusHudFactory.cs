using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.App;

internal static class VoiceStatusHudFactory
{
    /// <summary>Web HUD is opt-in only via <c>AMR_WEB_HUD=1</c> (WinForms host often clips height).</summary>
    public static bool ResolvePreferWeb(AppSettings settings) =>
        string.Equals(Environment.GetEnvironmentVariable("AMR_WEB_HUD"), "1", StringComparison.OrdinalIgnoreCase);

    public static IVoiceStatusHud Create(AppSettings settings)
    {
        if (!ResolvePreferWeb(settings))
        {
            return new VoiceStatusHud();
        }

        if (!WebView2RuntimeChecker.IsRuntimeAvailable())
        {
            Log.Information("Web status HUD skipped — WebView2 runtime not available; using native HUD");
            return new VoiceStatusHud();
        }

        var hudPage = Path.Combine(AppContext.BaseDirectory, "wwwroot", "hud.html");
        if (!File.Exists(hudPage))
        {
            Log.Warning("wwwroot/hud.html missing; using native HUD (rebuild UI with npm run build)");
            return new VoiceStatusHud();
        }

        try
        {
            return VoiceWebStatusHud.CreateSynchronously();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Web status HUD init failed; using native HUD");
            return new VoiceStatusHud();
        }
    }
}
