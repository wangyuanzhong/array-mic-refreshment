using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class PcmResamplerTests
{
    [Fact]
    public void To16kHzMono16Le_Sine440Hz_HasExpectedLengthAndRms()
    {
        const int sourceRate = 48000;
        const int channels = 2;
        var duration = TimeSpan.FromMilliseconds(500);
        var input = PcmResampler.GenerateSineWavePcm16(sourceRate, channels, 440, duration, amplitude: 0.6);

        var output = PcmResampler.To16kHzMono16Le(input, sourceRate, channels);

        var expectedSamples = (int)Math.Round(sourceRate * duration.TotalSeconds * (16000.0 / sourceRate));
        Assert.InRange(output.Length, expectedSamples * 2 - 8, expectedSamples * 2 + 8);

        var rms = PcmResampler.ComputeRms16Le(output);
        Assert.InRange(rms, 0.15, 0.55);
    }
}
