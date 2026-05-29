namespace ArrayMicRefreshment.Core;

/// <summary>
/// Combines an LLM API preset (by name) with skill/intent options for one-click pipeline configuration.
/// </summary>
public sealed class FeaturePreset
{
    public string Name { get; set; } = "默认";

    /// <summary>Matches <see cref="LlmPreset.Name"/> in <see cref="AppSettings.LlmPresets"/>.</summary>
    public string LlmPresetName { get; set; } = "预设1";

    public PromptIntent ForcedIntent { get; set; } = PromptIntent.PlainText;

    public OnRefineFailure OnRefineFailure { get; set; } = OnRefineFailure.UseRawTranscript;

    public List<string> OptionalOverlaySkills { get; set; } = new();
}
