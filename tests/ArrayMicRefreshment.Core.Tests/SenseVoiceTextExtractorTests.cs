using ArrayMicRefreshment.Asr;

namespace ArrayMicRefreshment.Core.Tests;

public class SenseVoiceTextExtractorTests
{
    [Theory]
    [InlineData("<|en|><|NEUTRAL|><|Speech|>hello world", "hello world")]
    [InlineData("<|zh|><|HAPPY|><|Speech|>你好", "你好")]
    [InlineData("plain text only", "plain text only")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("<|en|>", "")]
    [InlineData("<|en|>mixed<|NEUTRAL|> tags", "mixed tags")]
    public void ExtractPlainText_strips_sense_voice_tags(string raw, string expected)
    {
        var actual = SenseVoiceTextExtractor.ExtractPlainText(raw);
        Assert.Equal(expected, actual);
    }
}
