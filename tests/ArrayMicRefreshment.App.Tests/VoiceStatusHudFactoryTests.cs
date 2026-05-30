using ArrayMicRefreshment.App;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public sealed class VoiceStatusHudFactoryTests
{
    [Fact]
    public void ResolvePreferWeb_false_unless_env_one()
    {
        var previous = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        try
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", null);
            Assert.False(VoiceStatusHudFactory.ResolvePreferWeb(new AppSettings { UseWebStatusHud = true }));
            Assert.False(VoiceStatusHudFactory.ResolvePreferWeb(new AppSettings { UseWebStatusHud = false }));

            Environment.SetEnvironmentVariable("AMR_WEB_HUD", "1");
            Assert.True(VoiceStatusHudFactory.ResolvePreferWeb(new AppSettings { UseWebStatusHud = false }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", previous);
        }
    }

    [Fact]
    public void Create_without_env_returns_native_hud()
    {
        var previous = Environment.GetEnvironmentVariable("AMR_WEB_HUD");
        try
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", null);
            using var hud = VoiceStatusHudFactory.Create(new AppSettings { UseWebStatusHud = true });
            Assert.IsType<VoiceStatusHud>(hud);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AMR_WEB_HUD", previous);
        }
    }
}
