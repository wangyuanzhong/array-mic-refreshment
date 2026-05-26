using System.Text.Json.Serialization;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Web settings form draft (docs/UI_ROUTE_B_WEBVIEW2.md §7.3).</summary>
public sealed class SettingsDraftDto
{
    public bool MasterEnabled { get; set; } = true;

    public bool PasteToCaretEnabled { get; set; } = true;

    public bool LaunchAtStartup { get; set; } = true;

    public bool PromptRefineEnabled { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromptIntent ForcedIntent { get; set; } = PromptIntent.PlainText;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OnRefineFailure OnRefineFailure { get; set; } = OnRefineFailure.UseRawTranscript;

    public string? SelectedDeviceId { get; set; }

    public string? CurrentSpeakerUserId { get; set; }

    public float SpeakerVerifyThreshold { get; set; } = 0.40f;

    public string SelectedAsrModelId { get; set; } = string.Empty;

    public string SkillsDirectory { get; set; } = "skills";

    public string ModelsDirectory { get; set; } = "models";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VoiceTriggerMode TriggerMode { get; set; } = VoiceTriggerMode.PttOnly;

    public string WakeWordPhrase { get; set; } = "小助手";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WakeWordSensitivity WakeWordSensitivity { get; set; } = WakeWordSensitivity.Maximum;

    public int WakeCommandSilenceMs { get; set; } = 3000;

    public bool WakeUseVadEndDetection { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HudScreenCorner HudScreenCorner { get; set; } = HudScreenCorner.BottomRight;

    public string PttHotkey { get; set; } = "Ctrl+Alt+Space";

    public int SelectedLlmPresetIndex { get; set; }

    public List<LlmPresetDto> LlmPresets { get; set; } = new();

    public List<string> OptionalOverlaySkills { get; set; } = new();
}

public sealed class LlmPresetDto
{
    public string Name { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiModel { get; set; } = string.Empty;
}

public sealed class SettingsValidationErrorDto
{
    public string Field { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class SettingsValidationResultDto
{
    public bool Ok { get; set; }

    public List<SettingsValidationErrorDto> Errors { get; set; } = new();
}

public sealed class SaveSettingsResultDto
{
    public bool Ok { get; set; }

    public string? Error { get; set; }

    public string? Warning { get; set; }
}

public sealed class LlmTestResultDto
{
    public bool Ok { get; set; }

    public string Message { get; set; } = string.Empty;

    public long ElapsedMs { get; set; }

    public float RouterConfidence { get; set; }
}

public sealed class HotkeyCaptureResultDto
{
    public string Hotkey { get; set; } = string.Empty;

    public bool Cancelled { get; set; }
}
