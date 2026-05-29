using ArrayMicRefreshment.App;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public sealed class VoiceStatusHudFactoryTests
{
    [Fact]
    public void ResolvePreferWeb_uses_settings_when_env_unset()
    {
        var previous = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        try
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", null);
            var on = new AppSettings { UseWebStatusHud = true };
            var off = new AppSettings { UseWebStatusHud = false };
            Assert.True(VoiceStatusHudFactory.ResolvePreferWeb(on));
            Assert.False(VoiceStatusHudFactory.ResolvePreferWeb(off));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", previous);
        }
    }

    [Fact]
    public void ResolvePreferWeb_env_zero_overrides_settings()
    {
        var previous = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        try
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", "0");
            Assert.False(VoiceStatusHudFactory.ResolvePreferWeb(new AppSettings { UseWebStatusHud = true }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", previous);
        }
    }

    [Fact]
    public void ResolvePreferWeb_env_one_forces_web_preference()
    {
        var previous = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        try
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", "1");
            Assert.True(VoiceStatusHudFactory.ResolvePreferWeb(new AppSettings { UseWebStatusHud = false }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", previous);
        }
    }

    [Fact]
    public void Create_with_web_disabled_returns_native_hud()
    {
        var previous = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        try
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", "0");
            using var hud = VoiceStatusHudFactory.Create(new AppSettings { UseWebStatusHud = true });
            Assert.IsType<VoiceStatusHud>(hud);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", previous);
        }
    }
}
