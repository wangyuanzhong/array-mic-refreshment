using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Maps between <see cref="AppSettings"/> and <see cref="SettingsDraftDto"/>.</summary>
public static class SettingsDraftMapper
{
    public static SettingsDraftDto ToDraft(AppSettings settings, VoiceTriggerMode? runtimeTriggerMode)
    {
        settings.MigrateLegacyApiSettings();
        settings.MigrateLegacyFeaturePresets();

        return new SettingsDraftDto
        {
            MasterEnabled = settings.MasterEnabled,
            PasteToCaretEnabled = settings.PasteToCaretEnabled,
            LaunchAtStartup = settings.LaunchAtStartup,
            PromptRefineEnabled = settings.PromptRefineEnabled,
            ForcedIntent = settings.ForcedIntent,
            OnRefineFailure = settings.OnRefineFailure,
            SelectedDeviceId = settings.SelectedDeviceId,
            CurrentSpeakerUserId = settings.CurrentSpeakerUserId,
            SpeakerVerifyThreshold = settings.SpeakerVerifyThreshold,
            SelectedAsrModelId = settings.SelectedAsrModelId,
            SkillsDirectory = settings.SkillsDirectory,
            ModelsDirectory = settings.ModelsDirectory,
            TriggerMode = runtimeTriggerMode ?? settings.TriggerMode,
            WakeWordPhrase = settings.WakeWordPhrase,
            WakeWordSensitivity = settings.WakeWordSensitivity,
            WakeCommandSilenceMs = settings.WakeCommandSilenceMs,
            WakeUseVadEndDetection = settings.WakeUseVadEndDetection,
            HudScreenCorner = settings.HudScreenCorner,
            UseWebStatusHud = settings.UseWebStatusHud,
            PttHotkey = settings.PttHotkey,
            SelectedLlmPresetIndex = settings.SelectedLlmPresetIndex,
            LlmPresets = settings.LlmPresets
                .Select(p => new LlmPresetDto
                {
                    Name = p.Name,
                    ApiBaseUrl = p.ApiBaseUrl,
                    ApiKey = p.ApiKey,
                    ApiModel = p.ApiModel,
                })
                .ToList(),
            OptionalOverlaySkills = new List<string>(settings.OptionalOverlaySkills),
            SelectedFeaturePresetIndex = settings.SelectedFeaturePresetIndex,
            FeaturePresets = settings.FeaturePresets
                .Select(p => new FeaturePresetDto
                {
                    Name = p.Name,
                    LlmPresetName = p.LlmPresetName,
                    ForcedIntent = p.ForcedIntent,
                    OnRefineFailure = p.OnRefineFailure,
                    OptionalOverlaySkills = new List<string>(p.OptionalOverlaySkills),
                })
                .ToList(),
        };
    }

    public static AppSettings ToAppSettings(SettingsDraftDto draft, AppSettings? template = null)
    {
        var settings = template is null ? new AppSettings() : CloneTemplate(template);
        settings.MigrateLegacyApiSettings();
        settings.MigrateLegacyFeaturePresets();

        settings.MasterEnabled = draft.MasterEnabled;
        settings.PasteToCaretEnabled = draft.PasteToCaretEnabled;
        settings.LaunchAtStartup = draft.LaunchAtStartup;
        settings.SelectedDeviceId = draft.SelectedDeviceId;
        settings.CurrentSpeakerUserId = draft.CurrentSpeakerUserId;
        settings.SpeakerVerifyThreshold = Math.Clamp(draft.SpeakerVerifyThreshold, 0.25f, 0.85f);
        settings.SelectedAsrModelId = draft.SelectedAsrModelId ?? string.Empty;
        settings.SkillsDirectory = draft.SkillsDirectory?.Trim() ?? "skills";
        settings.ModelsDirectory = string.IsNullOrWhiteSpace(draft.ModelsDirectory)
            ? settings.ModelsDirectory
            : draft.ModelsDirectory.Trim();
        settings.TriggerMode = draft.TriggerMode;
        settings.WakeWordPhrase = draft.WakeWordPhrase?.Trim() ?? string.Empty;
        settings.WakeWordSensitivity = draft.WakeWordSensitivity;
        settings.WakeCommandSilenceMs = Math.Clamp(draft.WakeCommandSilenceMs, 800, 8000);
        settings.WakeUseVadEndDetection = draft.WakeUseVadEndDetection;
        settings.HudScreenCorner = draft.HudScreenCorner;
        settings.UseWebStatusHud = draft.UseWebStatusHud;
        settings.PttHotkey = draft.PttHotkey?.Trim() ?? settings.PttHotkey;
        settings.SelectedLlmPresetIndex = draft.SelectedLlmPresetIndex;

        if (draft.LlmPresets is { Count: > 0 })
        {
            settings.LlmPresets = draft.LlmPresets
                .Select(p => new LlmPreset
                {
                    Name = p.Name?.Trim() ?? string.Empty,
                    ApiBaseUrl = p.ApiBaseUrl?.Trim() ?? string.Empty,
                    ApiKey = p.ApiKey ?? string.Empty,
                    ApiModel = p.ApiModel?.Trim() ?? string.Empty,
                })
                .ToList();
        }

        var presetIdx = Math.Clamp(
            settings.SelectedLlmPresetIndex,
            0,
            Math.Max(0, settings.LlmPresets.Count - 1));
        settings.SelectedLlmPresetIndex = presetIdx;

        if (settings.LlmPresets.Count > 0)
        {
            var current = settings.LlmPresets[presetIdx];
            settings.ApiBaseUrl = current.ApiBaseUrl;
            settings.ApiKey = current.ApiKey;
            settings.ApiModel = current.ApiModel;
        }

        if (draft.FeaturePresets is { Count: > 0 })
        {
            settings.FeaturePresets = draft.FeaturePresets
                .Select(p => new FeaturePreset
                {
                    Name = p.Name?.Trim() ?? string.Empty,
                    LlmPresetName = p.LlmPresetName?.Trim() ?? string.Empty,
                    ForcedIntent = p.ForcedIntent,
                    OnRefineFailure = p.OnRefineFailure,
                    OptionalOverlaySkills = p.OptionalOverlaySkills
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                })
                .ToList();
            settings.SelectedFeaturePresetIndex = draft.SelectedFeaturePresetIndex;

            // Web UI edits top-level refine fields; mirror them into the active preset before apply.
            var activePresetIndex = Math.Clamp(
                settings.SelectedFeaturePresetIndex,
                0,
                Math.Max(0, settings.FeaturePresets.Count - 1));
            var activePreset = settings.FeaturePresets[activePresetIndex];
            activePreset.ForcedIntent = draft.ForcedIntent;
            activePreset.OnRefineFailure = draft.OnRefineFailure;
            activePreset.OptionalOverlaySkills = draft.OptionalOverlaySkills
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            FeaturePresetApplier.ApplyFeaturePreset(settings, activePresetIndex);
        }
        else
        {
            settings.PromptRefineEnabled = draft.PromptRefineEnabled;
            settings.ForcedIntent = draft.ForcedIntent;
            settings.OnRefineFailure = draft.OnRefineFailure;
            settings.OptionalOverlaySkills = draft.OptionalOverlaySkills
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (template is not null)
        {
            settings.PrivacyAcceptedHost = template.PrivacyAcceptedHost;
        }

        return settings;
    }

    private static AppSettings CloneTemplate(AppSettings template)
    {
        var clone = new AppSettings();
        SettingsCopier.CopyInto(template, clone);
        return clone;
    }
}
