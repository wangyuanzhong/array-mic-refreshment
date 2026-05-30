using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public class WakeWordPhraseEncodingTests
{
    [Theory]
    [InlineData("小助手")]
    [InlineData("小德小德")]
    [InlineData("你好")]
    [InlineData("蛋哥蛋哥")]
    public void CanEncode_builtin_phrases_without_model(string phrase)
    {
        var ok = WakeWordPhraseEncoding.CanEncode(
            modelsDirectory: "models-missing-for-test",
            phrase,
            WakeWordSensitivity.High,
            out var error);

        Assert.True(ok, error);
    }

    [Fact]
    public void CanEncode_rejects_non_chinese()
    {
        var ok = WakeWordPhraseEncoding.CanEncode(
            "models",
            "hello",
            WakeWordSensitivity.High,
            out var error);

        Assert.False(ok);
        Assert.Contains("中文", error ?? string.Empty);
    }
}
