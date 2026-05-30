using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Audio.Tests;

public class WakeWordDictationSilenceTests
{
    [Fact]
    public void ShouldEndAfterQuietBelowExtend_honors_configured_silence()
    {
        var quietSince = new DateTimeOffset(2026, 5, 26, 0, 0, 0, TimeSpan.Zero);
        var timeout = TimeSpan.FromMilliseconds(2000);

        Assert.False(
            WakeWordDictationSilence.ShouldEndAfterQuietBelowExtend(
                quietSince.AddMilliseconds(1999),
                quietSince,
                timeout));
        Assert.True(
            WakeWordDictationSilence.ShouldEndAfterQuietBelowExtend(
                quietSince.AddMilliseconds(2000),
                quietSince,
                timeout));
    }

    [Fact]
    public void ShouldEndAfterQuietBelowExtend_false_when_not_yet_quiet()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(
            WakeWordDictationSilence.ShouldEndAfterQuietBelowExtend(now, quietBelowExtendSinceUtc: null, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ShouldEndAfterVadSilence_honors_configured_silence()
    {
        var lastSpeech = new DateTimeOffset(2026, 5, 26, 0, 0, 0, TimeSpan.Zero);
        var timeout = TimeSpan.FromMilliseconds(2000);

        Assert.False(
            WakeWordDictationSilence.ShouldEndAfterVadSilence(
                lastSpeech.AddMilliseconds(1999),
                lastSpeech,
                hadSpeech: true,
                timeout));
        Assert.True(
            WakeWordDictationSilence.ShouldEndAfterVadSilence(
                lastSpeech.AddMilliseconds(2000),
                lastSpeech,
                hadSpeech: true,
                timeout));
    }

    [Fact]
    public void ShouldEndAfterVocalActivityGap_ends_without_low_energy()
    {
        var lastVocal = new DateTimeOffset(2026, 5, 26, 0, 0, 0, TimeSpan.Zero);
        var timeout = TimeSpan.FromSeconds(2);

        Assert.True(
            WakeWordDictationSilence.ShouldEndAfterVocalActivityGap(
                lastVocal.AddSeconds(2),
                lastVocal,
                heardSpeech: true,
                timeout));
    }
}
