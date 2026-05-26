namespace ArrayMicRefreshment.App;

/// <summary>Live voice-session phase — drives tray icon color and the status HUD.</summary>
internal enum VoiceActivityPhase
{
    Idle,
    WakePrompt,
    Recording,
    Recognizing,
    Error,
}
