using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

internal static class SettingsCopier
{
    public static void CopyInto(AppSettings source, AppSettings target)
    {
        target.MasterEnabled = source.MasterEnabled;
        target.PasteToCaretEnabled = source.PasteToCaretEnabled;
        target.LaunchAtStartup = source.LaunchAtStartup;
        target.PromptRefineEnabled = source.PromptRefineEnabled;
        target.ForcedIntent = source.ForcedIntent;
        target.OnRefineFailure = source.OnRefineFailure;
        target.SelectedDeviceId = source.SelectedDeviceId;
        target.CurrentSpeakerUserId = source.CurrentSpeakerUserId;
        target.PttHotkey = source.PttHotkey;
        target.TriggerMode = source.TriggerMode;
        target.WakeWordPhrase = source.WakeWordPhrase;
        target.WakeWordSensitivity = source.WakeWordSensitivity;
        target.WakeCommandSilenceMs = source.WakeCommandSilenceMs;
        target.WakeUseVadEndDetection = source.WakeUseVadEndDetection;
        target.HudScreenCorner = source.HudScreenCorner;
        target.UseWebStatusHud = source.UseWebStatusHud;
        target.SkillsDirectory = source.SkillsDirectory;
        target.ModelsDirectory = source.ModelsDirectory;
        target.SelectedAsrModelId = source.SelectedAsrModelId;
        target.SpeakerVerifyThreshold = source.SpeakerVerifyThreshold;
        target.PrivacyAcceptedHost = source.PrivacyAcceptedHost;
        target.OptionalOverlaySkills = new List<string>(source.OptionalOverlaySkills);

        // Feature presets
        target.SelectedFeaturePresetIndex = source.SelectedFeaturePresetIndex;
        target.FeaturePresets = source.FeaturePresets?.Count > 0
            ? source.FeaturePresets
                .Select(p => new FeaturePreset
                {
                    Name = p.Name,
                    LlmPresetName = p.LlmPresetName,
                    ForcedIntent = p.ForcedIntent,
                    OnRefineFailure = p.OnRefineFailure,
                    OptionalOverlaySkills = new List<string>(p.OptionalOverlaySkills),
                })
                .ToList()
            : new List<FeaturePreset>
            {
                new()
                {
                    Name = "默认",
                    LlmPresetName = source.CurrentPreset.Name,
                    ForcedIntent = source.ForcedIntent,
                    OnRefineFailure = source.OnRefineFailure,
                    OptionalOverlaySkills = new List<string>(source.OptionalOverlaySkills),
                },
            };

        // Multi-preset LLM configuration
        target.SelectedLlmPresetIndex = source.SelectedLlmPresetIndex;
        target.LlmPresets = source.LlmPresets?.Count > 0
            ? source.LlmPresets
                .Select(p => new LlmPreset
                {
                    Name = p.Name,
                    ApiBaseUrl = p.ApiBaseUrl,
                    ApiKey = p.ApiKey,
                    ApiModel = p.ApiModel,
                })
                .ToList()
            : new List<LlmPreset>
            {
                new() { Name = "预设1", ApiBaseUrl = source.ApiBaseUrl, ApiKey = source.ApiKey, ApiModel = source.ApiModel },
                new() { Name = "预设2" },
                new() { Name = "预设3" },
            };

        // Keep legacy fields in sync for backward compatibility.
        var current = target.CurrentPreset;
        target.ApiBaseUrl = current.ApiBaseUrl;
        target.ApiKey = current.ApiKey;
        target.ApiModel = current.ApiModel;
    }

    public static bool RequiresSherpaReload(AppSettings previous, AppSettings next) =>
        !string.Equals(previous.ModelsDirectory, next.ModelsDirectory, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(previous.SelectedAsrModelId, next.SelectedAsrModelId, StringComparison.OrdinalIgnoreCase);

    public static bool RequiresWakeCaptureRestart(AppSettings previous, AppSettings next) =>
        previous.TriggerMode != next.TriggerMode
        || !string.Equals(previous.SelectedDeviceId, next.SelectedDeviceId, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(previous.WakeWordPhrase, next.WakeWordPhrase, StringComparison.Ordinal);

    public static bool RequiresPipelineRebuild(AppSettings previous, AppSettings next) =>
        RequiresSherpaReload(previous, next)
        || previous.PromptRefineEnabled != next.PromptRefineEnabled
        || previous.ForcedIntent != next.ForcedIntent
        || !string.Equals(previous.SkillsDirectory, next.SkillsDirectory, StringComparison.OrdinalIgnoreCase)
        || previous.OnRefineFailure != next.OnRefineFailure
        || previous.SelectedFeaturePresetIndex != next.SelectedFeaturePresetIndex
        || previous.SelectedLlmPresetIndex != next.SelectedLlmPresetIndex
        || !SamePreset(previous.CurrentPreset, next.CurrentPreset)
        || !Enumerable.SequenceEqual(
            previous.OptionalOverlaySkills.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            next.OptionalOverlaySkills.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    private static bool SamePreset(LlmPreset a, LlmPreset b) =>
        string.Equals(a.ApiBaseUrl, b.ApiBaseUrl, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ApiKey, b.ApiKey, StringComparison.Ordinal)
        && string.Equals(a.ApiModel, b.ApiModel, StringComparison.Ordinal);
}
