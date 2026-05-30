namespace ArrayMicRefreshment.Prompt;

/// <summary>Built-in refinement styles from shipped <c>skills/manifest.yaml</c> (fallback when manifest cannot be read).</summary>
public static class RefinementStyleDefaults
{
    public static IReadOnlyList<RefinementStyleService.RefinementStyleEntry> BuiltinEntries { get; } =
    [
        new("plain-text", "纯文本整理", "去除口误、加标点、修正语序，输出适合人类阅读的书面语（短 prompt，省 token）", false, null),
        new("general-ai", "通用 AI Prompt", "把语音整理成通用 AI 提示词（不扩写）", false, null),
        new("code-editing", "软件开发需求（产品视角）", "口述需求 → 页面/流程/步骤化说明；不写接口、框架、函数名（避免误导）", false, null),
        new("research", "深度研究 Prompt", "同语言、多角度拆解的长研究提示词（可适度拓宽主题）", false, null),
        new("task-plan", "待办列表", "口述 → 简短可执行待办（动词开头）；不是产品需求长文，也不是深度研究", false, null),
    ];
}
