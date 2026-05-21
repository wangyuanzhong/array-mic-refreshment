using ArrayMicRefreshment.Core.Audio;

namespace ArrayMicRefreshment.Core.Tests;

public class PcmConvertersTests
{
    [Fact]
    public void Resample_doubles_sample_count_when_halving_rate()
    {
        var input = new float[] { 0f, 1f, 0f, -1f };
        var output = PcmConverters.Resample(input, 16000, 8000);
        Assert.Equal(2, output.Length);
    }

    [Fact]
    public void Ensure16KHzMono_leaves_16k_buffer_unchanged_length()
    {
        var pcm = new byte[320];
        var result = PcmConverters.Ensure16KHzMonoPcm16Le(pcm, 16000);
        Assert.Equal(320, result.Length);
    }
}
