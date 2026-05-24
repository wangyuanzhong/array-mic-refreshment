using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Prompt;

public static class SpecialistKeyMapper
{
    public static PromptIntent FromUpstreamIntent(string? upstreamIntent, IReadOnlyDictionary<string, string> intentMap)
    {
        if (string.IsNullOrWhiteSpace(upstreamIntent))
        {
            return PromptIntent.GeneralAi;
        }

        if (!intentMap.TryGetValue(upstreamIntent.Trim(), out var specialistKey))
        {
            return PromptIntent.GeneralAi;
        }

        return FromSpecialistKey(specialistKey);
    }

    public static PromptIntent FromSpecialistKey(string? specialistKey) =>
        specialistKey?.Trim().ToLowerInvariant() switch
        {
            "plain-text" => PromptIntent.PlainText,
            "code-editing" => PromptIntent.CodeEditing,
            "general-ai" => PromptIntent.GeneralAi,
            "research" => PromptIntent.Research,
            "task-plan" => PromptIntent.TaskPlan,
            _ => PromptIntent.GeneralAi,
        };

    public static string ToSpecialistKey(PromptIntent intent) =>
        intent switch
        {
            PromptIntent.PlainText => "plain-text",
            PromptIntent.CodeEditing => "code-editing",
            PromptIntent.GeneralAi => "general-ai",
            PromptIntent.Research => "research",
            PromptIntent.TaskPlan => "task-plan",
            _ => "general-ai",
        };
}
