using System.Text.Json.Serialization;

namespace ArrayMicRefreshment.Core;

public enum PromptIntent
{
    /// <summary>Let the router decide (legacy behaviour).</summary>
    Auto,
    /// <summary>Plain-text transcript polish — remove fillers, fix grammar, add punctuation.</summary>
    PlainText,
    /// <summary>Turn speech into a general-purpose AI prompt.</summary>
    GeneralAi,
    /// <summary>Turn speech into precise code-editing instructions.</summary>
    CodeEditing,
    /// <summary>Turn speech into a deep-research prompt.</summary>
    Research,
    /// <summary>Turn speech into a to-do list.</summary>
    TaskPlan,
}

public enum OnRefineFailure
{
    UseRawTranscript,
    ShowError,
    KeepLast,
}

/// <summary>A single LLM API preset (URL + Key + Model).</summary>
public sealed class LlmPreset
{
    public string Name { get; set; } = "未命名";
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiModel { get; set; } = "gpt-4o-mini";
}

public sealed class AppSettings
{
    public bool MasterEnabled { get; set; } = true;
    public bool PasteToCaretEnabled { get; set; } = true;
    /// <summary>Register the app in the current user's Windows Run key on logon.</summary>
    public bool LaunchAtStartup { get; set; } = true;
    public bool PromptRefineEnabled { get; set; }
    public PromptIntent ForcedIntent { get; set; } = PromptIntent.PlainText;

    /// <summary>
    /// Specialist key for forced refinement (<c>auto</c>, <c>plain-text</c>, <c>code-editing</c>, or user style id).
    /// When empty, legacy <see cref="ForcedIntent"/> is used.
    /// </summary>
    public string ForcedSpecialistKey { get; set; } = string.Empty;

    public OnRefineFailure OnRefineFailure { get; set; } = OnRefineFailure.UseRawTranscript;

    public string? SelectedDeviceId { get; set; }
    public string? CurrentSpeakerUserId { get; set; }

    public string PttHotkey { get; set; } = "Ctrl+Alt+Space";

    /// <summary>Hold hotkey to record, or toggle start/stop on each hotkey press.</summary>
    /// <summary>PTT hold-to-talk, wake-word, both, or manual hotkey toggle. Missing in old JSON defaults to <see cref="VoiceTriggerMode.PttOnly"/>.</summary>
    public VoiceTriggerMode TriggerMode { get; set; } = VoiceTriggerMode.PttOnly;

    /// <summary>Wake phrase text when <see cref="TriggerMode"/> is <see cref="VoiceTriggerMode.WakeWordOnly"/>.</summary>
    public string WakeWordPhrase { get; set; } = "小助手";

    /// <summary>KWS AGC profile for quiet environments.</summary>
    public WakeWordSensitivity WakeWordSensitivity { get; set; } = WakeWordSensitivity.Maximum;

    /// <summary>Legacy JSON field; wake end timing uses <see cref="WakeWordCaptureDefaults.CommandEndSilenceMs"/>.</summary>
    [Obsolete("Wake end silence is fixed in WakeWordCaptureDefaults; not shown in settings UI.")]
    public int WakeCommandSilenceMs { get; set; } = WakeWordCaptureDefaults.CommandEndSilenceMs;

    /// <summary>Legacy JSON field; Silero VAD is used automatically when the model file exists.</summary>
    [Obsolete("VAD end detection is automatic when models/silero_vad.onnx is present.")]
    public bool WakeUseVadEndDetection { get; set; } = true;

    /// <summary>Screen corner for the live voice status HUD.</summary>
    public HudScreenCorner HudScreenCorner { get; set; } = HudScreenCorner.BottomRight;

    /// <summary>
    /// Use transparent WebView2 HUD (<c>#/hud</c>) instead of native WinForms overlay.
    /// Falls back to native HUD when WebView2 or wwwroot is unavailable. Takes effect on app restart.
    /// Override with env <c>AMR_WEB_HUD=0|1</c>.
    /// </summary>
    public bool UseWebStatusHud { get; set; } = false;

    /// <summary>Last Web settings window client width (px).</summary>
    public int SettingsWindowWidth { get; set; } = 960;

    /// <summary>Last Web settings window client height (px).</summary>
    public int SettingsWindowHeight { get; set; } = 720;

    public string SkillsDirectory { get; set; } = "skills";
    public string ModelsDirectory { get; set; } = "models";
    public string SelectedAsrModelId { get; set; } = string.Empty;
    public float SpeakerVerifyThreshold { get; set; } = 0.40f;
    public string? PrivacyAcceptedHost { get; set; }
    public List<string> OptionalOverlaySkills { get; set; } = new();

    // ---- Multi-preset LLM configuration ----

    /// <summary>Index of the currently active preset in <see cref="LlmPresets"/>.</summary>
    public int SelectedLlmPresetIndex { get; set; } = 0;

    /// <summary>Three presets so the user can switch between local / cloud / backup APIs quickly.</summary>
    public List<LlmPreset> LlmPresets { get; set; } = new()
    {
        new LlmPreset { Name = "预设1" },
        new LlmPreset { Name = "预设2" },
        new LlmPreset { Name = "预设3" },
    };

    // ---- Feature presets (LLM preset + skill/intent bundle) ----

    /// <summary>Index of the active feature preset in <see cref="FeaturePresets"/>.</summary>
    public int SelectedFeaturePresetIndex { get; set; } = 0;

    /// <summary>Named bundles combining an LLM preset with intent/overlay options.</summary>
    public List<FeaturePreset> FeaturePresets { get; set; } = new();

    // ---- Legacy fields (kept for backward-compatible JSON deserialization) ----

    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Returns the currently active preset.  If the list is empty or index is out of range,
    /// falls back to the legacy single-preset fields so existing users are not broken.
    /// </summary>
    [JsonIgnore]
    public LlmPreset CurrentPreset
    {
        get
        {
            if (LlmPresets is not null &&
                SelectedLlmPresetIndex >= 0 &&
                SelectedLlmPresetIndex < LlmPresets.Count)
            {
                return LlmPresets[SelectedLlmPresetIndex];
            }

            // Fallback for settings created before the multi-preset feature.
            return new LlmPreset
            {
                Name = "默认",
                ApiBaseUrl = ApiBaseUrl,
                ApiKey = ApiKey,
                ApiModel = ApiModel,
            };
        }
    }

    /// <summary>
    /// One-time migration: copy the legacy single-preset fields into preset 0
    /// so existing users keep working after the upgrade.
    /// </summary>
    public void MigrateLegacyApiSettings()
    {
        if (LlmPresets is null || LlmPresets.Count == 0)
        {
            LlmPresets = new List<LlmPreset>
            {
                new() { Name = "预设1", ApiBaseUrl = ApiBaseUrl, ApiKey = ApiKey, ApiModel = ApiModel },
                new() { Name = "预设2" },
                new() { Name = "预设3" },
            };
            return;
        }

        // If preset 0 still has default values and legacy fields have real data, migrate.
        var p0 = LlmPresets[0];
        if (p0.ApiBaseUrl == "https://api.openai.com/v1" &&
            !string.IsNullOrWhiteSpace(ApiBaseUrl) &&
            ApiBaseUrl != "https://api.openai.com/v1")
        {
            p0.ApiBaseUrl = ApiBaseUrl;
            p0.ApiKey = ApiKey;
            p0.ApiModel = ApiModel;
        }

        // Ensure legacy fields always match the current preset so components
        // that read ApiBaseUrl / ApiKey / ApiModel directly still work.
        var current = CurrentPreset;
        ApiBaseUrl = current.ApiBaseUrl;
        ApiKey = current.ApiKey;
        ApiModel = current.ApiModel;
    }

    /// <summary>
    /// One-time migration: when <see cref="FeaturePresets"/> is missing from JSON, build a single
    /// default preset from legacy <see cref="PromptRefineEnabled"/> / intent / overlay fields.
    /// </summary>
    public void MigrateLegacyFeaturePresets()
    {
        if (FeaturePresets is { Count: > 0 })
        {
            SelectedFeaturePresetIndex = Math.Clamp(
                SelectedFeaturePresetIndex,
                0,
                FeaturePresets.Count - 1);
            return;
        }

        MigrateLegacyApiSettings();

        var llmName = CurrentPreset.Name;
        FeaturePresets =
        [
            new FeaturePreset
            {
                Name = "默认",
                LlmPresetName = llmName,
                ForcedIntent = ForcedIntent,
                OnRefineFailure = OnRefineFailure,
                OptionalOverlaySkills = new List<string>(OptionalOverlaySkills),
            },
        ];
        SelectedFeaturePresetIndex = 0;
    }
}
