using ArrayMicRefreshment.Asr;

namespace ArrayMicRefreshment.Core.Tests;

public class AsrInferenceTuningTests
{
    [Theory]
    [InlineData(4, 2)]
    [InlineData(8, 4)]
    [InlineData(12, 6)]
    [InlineData(16, 8)]
    [InlineData(32, 8)]
    public void ResolveNumThreads_clamps_to_expected_range(int logicalCores, int expected)
    {
        var actual = AsrInferenceTuning.ResolveNumThreadsForProcessorCount(logicalCores);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Qwen_token_limits_support_long_utterances()
    {
        Assert.True(AsrInferenceTuning.QwenMaxNewTokens >= 512);
        Assert.True(AsrInferenceTuning.QwenMaxTotalLen >= AsrInferenceTuning.QwenMaxNewTokens);
    }
}
