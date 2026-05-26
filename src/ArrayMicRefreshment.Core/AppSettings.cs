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
    public OnRefineFailure OnRefineFailure { get; set; } = OnRefineFailure.UseRawTranscript;

    public string? SelectedDeviceId { get; set; }
    public string? CurrentSpeakerUserId { get; set; }

    public string PttHotkey { get; set; } = "Ctrl+Alt+Space";

    /// <summary>PTT hotkey or wake-word hands-free mode. Missing in old JSON defaults to <see cref="VoiceTriggerMode.PttOnly"/>.</summary>
    public VoiceTriggerMode TriggerMode { get; set; } = VoiceTriggerMode.PttOnly;

    /// <summary>Wake phrase text when <see cref="TriggerMode"/> is <see cref="VoiceTriggerMode.WakeWordOnly"/>.</summary>
    public string WakeWordPhrase { get; set; } = "小助手";

    /// <summary>KWS AGC profile for quiet environments.</summary>
    public WakeWordSensitivity WakeWordSensitivity { get; set; } = WakeWordSensitivity.Maximum;

    /// <summary>Silence after last speech chunk before wake command is sent to ASR (ms).</summary>
    public int WakeCommandSilenceMs { get; set; } = 3000;

    /// <summary>Use VAD tail analysis (in addition to silence timeout) to end wake dictation.</summary>
    public bool WakeUseVadEndDetection { get; set; } = true;

    /// <summary>Screen corner for the live voice status HUD.</summary>
    public HudScreenCorner HudScreenCorner { get; set; } = HudScreenCorner.BottomRight;

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
}
