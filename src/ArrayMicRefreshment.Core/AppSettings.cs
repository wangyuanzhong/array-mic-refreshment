namespace ArrayMicRefreshment.Core;

public enum PromptIntent
{
    Auto,
    CodeEditing,
    GeneralAi,
    Research,
    TaskPlan,
}

public enum OnRefineFailure
{
    UseRawTranscript,
    ShowError,
    KeepLast,
}

public sealed class AppSettings
{
    public bool MasterEnabled { get; set; } = true;
    public bool PasteToCaretEnabled { get; set; }
    public bool PromptRefineEnabled { get; set; }
    public PromptIntent ForcedIntent { get; set; } = PromptIntent.Auto;
    public OnRefineFailure OnRefineFailure { get; set; } = OnRefineFailure.UseRawTranscript;

    public string? SelectedDeviceId { get; set; }
    public string? CurrentSpeakerUserId { get; set; }

    public string PttHotkey { get; set; } = "Ctrl+Shift+Space";

    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiModel { get; set; } = "gpt-4o-mini";
    public string SkillsDirectory { get; set; } = "skills";

    public string ModelsDirectory { get; set; } = "models";

    /// <summary>Cosine similarity threshold for speaker verification (default 0.5).</summary>
    public float SpeakerVerifyThreshold { get; set; } = 0.5f;
}
