using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.App;

internal static class VoiceStatusHudFactory
{
    /// <summary>
    /// Resolves whether to use the WebView2 HUD experiment.
    /// <c>AMR_WEB_HUD=0</c> forces native; <c>AMR_WEB_HUD=1</c> forces Web when runtime exists.
    /// </summary>
    public static bool ResolvePreferWeb(AppSettings settings)
    {
        var env = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        if (string.Equals(env, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return settings.UseWebStatusHud;
    }

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
