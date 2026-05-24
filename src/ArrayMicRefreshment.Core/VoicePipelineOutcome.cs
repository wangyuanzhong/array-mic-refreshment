namespace ArrayMicRefreshment.Core;

public enum VoicePipelineStatus
{
    Emitted,
    EmittedRawFallback,
    SkippedMasterDisabled,
    SpeakerRejected,
    EmptyTranscript,
}

public readonly record struct VoicePipelineOutcome(
    VoicePipelineStatus Status,
    string? Detail = null,
    string? AsrModelId = null,
    bool RefineApplied = false,
    string? RefineStatus = null);
