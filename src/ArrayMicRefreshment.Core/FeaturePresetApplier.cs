namespace ArrayMicRefreshment.Core;

/// <summary>Applies a <see cref="FeaturePreset"/> to the active pipeline fields on <see cref="AppSettings"/>.</summary>
public static class FeaturePresetApplier
{
    public static void ApplyFeaturePreset(AppSettings settings, int index)
    {
        if (settings.FeaturePresets is null || settings.FeaturePresets.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index, 0, settings.FeaturePresets.Count - 1);
        settings.SelectedFeaturePresetIndex = index;
        var preset = settings.FeaturePresets[index];

        var llmIdx = settings.LlmPresets?.FindIndex(p =>
            string.Equals(p.Name, preset.LlmPresetName, StringComparison.OrdinalIgnoreCase)) ?? -1;
        if (llmIdx >= 0)
        {
            settings.SelectedLlmPresetIndex = llmIdx;
        }

        settings.ForcedIntent = preset.ForcedIntent;
        settings.ForcedSpecialistKey = preset.ForcedSpecialistKey;
        settings.OnRefineFailure = preset.OnRefineFailure;
        settings.OptionalOverlaySkills = preset.OptionalOverlaySkills
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.PromptRefineEnabled = true;

        var current = settings.CurrentPreset;
        settings.ApiBaseUrl = current.ApiBaseUrl;
        settings.ApiKey = current.ApiKey;
        settings.ApiModel = current.ApiModel;
    }
}
