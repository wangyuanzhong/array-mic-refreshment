using ArrayMicRefreshment.App.Web;
using Xunit;

namespace ArrayMicRefreshment.App.Tests;

public sealed class WebViewDpiScalingTests
{
    [Theory]
    [InlineData(960, 720, 1f, 960, 720)]
    [InlineData(960, 720, 1.25f, 1200, 900)]
    [InlineData(640, 480, 1.5f, 960, 720)]
    public void ScaleLogicalSize_UsesBaseline96Dpi(int w, int h, float scale, int expectedW, int expectedH)
    {
        var size = WebViewDpiScaling.ScaleLogicalSize(w, h, scale);
        Assert.Equal(expectedW, size.Width);
        Assert.Equal(expectedH, size.Height);
    }

    [Fact]
    public void ToLogicalSize_RoundTripsPhysicalClientSize()
    {
        const float scale = 1.25f;
        var physical = WebViewDpiScaling.ScaleLogicalSize(960, 720, scale);
        var logical = WebViewDpiScaling.ToLogicalSize(physical, scale);
        Assert.Equal(960, logical.Width);
        Assert.Equal(720, logical.Height);
    }
}
