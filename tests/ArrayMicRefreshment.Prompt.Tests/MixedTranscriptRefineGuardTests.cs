using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Prompt.Tests;

public class MixedTranscriptRefineGuardTests
{
    [Theory]
    [InlineData("今天天气不错", false)]
    [InlineData("把 deploy 到 staging", true)]
    [InlineData("用React的useEffect", true)]
    public void LooksMixed_detects_latin_script(string text, bool expected) =>
        Assert.Equal(expected, MixedTranscriptRefineGuard.LooksMixed(text));

    [Fact]
    public void ExtractEnglishTokens_finds_distinct_tokens()
    {
        var tokens = MixedTranscriptRefineGuard.ExtractEnglishTokens("把 deploy 到 staging，React 的 useEffect");
        Assert.Contains("deploy", tokens);
        Assert.Contains("staging", tokens);
        Assert.Contains("React", tokens);
        Assert.Contains("useEffect", tokens);
        Assert.Equal(4, tokens.Count);
    }

    [Fact]
    public void PassesValidation_accepts_when_english_tokens_preserved()
    {
        var raw = "嗯把 deploy 到 staging";
        var refined = "把 deploy 到 staging。";
        var tokens = MixedTranscriptRefineGuard.ExtractEnglishTokens(raw);
        Assert.True(MixedTranscriptRefineGuard.PassesValidation(raw, refined, tokens));
    }

    [Fact]
    public void PassesValidation_rejects_when_english_tokens_translated_away()
    {
        var raw = "把 deploy 到 staging";
        var refined = "把部署到预发环境";
        var tokens = MixedTranscriptRefineGuard.ExtractEnglishTokens(raw);
        Assert.False(MixedTranscriptRefineGuard.PassesValidation(raw, refined, tokens));
    }

    [Fact]
    public void AppendProtectionHint_lists_tokens()
    {
        var hint = MixedTranscriptRefineGuard.AppendProtectionHint("hello", ["React", "K8s"]);
        Assert.Contains("React", hint);
        Assert.Contains("K8s", hint);
    }
}
