using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Audio.Tests;

public class WakeWordDictationSilenceTests
{
    [Fact]
    public void ShouldEnd_honors_configured_silence_timeout()
    {
        var lastVocal = new DateTimeOffset(2026, 5, 26, 0, 0, 0, TimeSpan.Zero);
        var timeout = TimeSpan.FromMilliseconds(2000);

        Assert.False(WakeWordDictationSilence.ShouldEnd(lastVocal.AddMilliseconds(1999), lastVocal, timeout));
        Assert.True(WakeWordDictationSilence.ShouldEnd(lastVocal.AddMilliseconds(2000), lastVocal, timeout));
        Assert.True(WakeWordDictationSilence.ShouldEnd(lastVocal.AddMilliseconds(5000), lastVocal, timeout));
    }
}
