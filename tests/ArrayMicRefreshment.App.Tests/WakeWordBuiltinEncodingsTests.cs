using ArrayMicRefreshment.Asr;

namespace ArrayMicRefreshment.App.Tests;

public sealed class WakeWordBuiltinEncodingsTests
{
    [Fact]
    public void TryGetLine_returns_default_phrase()
    {
        Assert.True(WakeWordBuiltinEncodings.TryGetLine("小助手", out var line));
        Assert.Contains("@小助手", line, StringComparison.Ordinal);
        Assert.Contains("x iǎo", line, StringComparison.Ordinal);
    }
}
