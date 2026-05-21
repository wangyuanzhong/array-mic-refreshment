using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Prompt.Tests;

public class SpecialistKeyMapperTests
{
    private static readonly Dictionary<string, string> IntentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["write_code"] = "code-editing",
        ["general_chat"] = "general-ai",
        ["summarize"] = "research",
        ["create_file"] = "task-plan",
    };

    [Theory]
    [InlineData("write_code", PromptIntent.CodeEditing)]
    [InlineData("general_chat", PromptIntent.GeneralAi)]
    [InlineData("summarize", PromptIntent.Research)]
    [InlineData("create_file", PromptIntent.TaskPlan)]
    public void FromUpstreamIntent_maps_known_intents(string upstream, PromptIntent expected)
    {
        var intent = SpecialistKeyMapper.FromUpstreamIntent(upstream, IntentMap);
        Assert.Equal(expected, intent);
    }

    [Fact]
    public void FromUpstreamIntent_unknown_returns_GeneralAi()
    {
        Assert.Equal(PromptIntent.GeneralAi, SpecialistKeyMapper.FromUpstreamIntent("nope", IntentMap));
    }

    [Theory]
    [InlineData("code-editing", PromptIntent.CodeEditing)]
    [InlineData("unknown-key", PromptIntent.GeneralAi)]
    public void FromSpecialistKey_maps_keys(string key, PromptIntent expected)
    {
        Assert.Equal(expected, SpecialistKeyMapper.FromSpecialistKey(key));
    }
}
