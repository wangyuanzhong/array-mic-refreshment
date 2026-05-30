namespace ArrayMicRefreshment.Core;

/// <summary>Runtime voice capture routing mode (persisted in <see cref="AppSettings.TriggerMode"/>).</summary>
public enum VoiceTriggerMode
{
    /// <summary>Global PTT hotkey only (default, existing behaviour).</summary>
    PttOnly,

    /// <summary>Wake-word listening only; PTT utterances are ignored at the orchestrator.</summary>
    WakeWordOnly,

    /// <summary>PTT and wake-word both active; PTT takes priority while held.</summary>
    Both,

    /// <summary>
    /// Same global hotkey as PTT, but press once to start recording and press again to stop
    /// (release does not end the session). Wake-word is off.
    /// </summary>
    Manual,
}

/// <summary>Which path produced an <see cref="AudioUtterance"/> for the shared pipeline.</summary>
public enum VoiceTriggerKind
{
    Ptt,
    WakeWord,
}

public sealed class WakeWordDetectedEventArgs : EventArgs
{
    public WakeWordDetectedEventArgs(string keyword, DateTimeOffset detectedAtUtc)
    {
        Keyword = keyword;
        DetectedAtUtc = detectedAtUtc;
    }

    public string Keyword { get; }
    public DateTimeOffset DetectedAtUtc { get; }
}

public sealed class UtteranceCaptureEventArgs : EventArgs
{
    public UtteranceCaptureEventArgs(AudioUtterance utterance, VoiceTriggerKind triggerKind)
    {
        Utterance = utterance;
        TriggerKind = triggerKind;
    }

    public AudioUtterance Utterance { get; }
    public VoiceTriggerKind TriggerKind { get; }
}

/// <summary>
/// Streams 16 kHz mono PCM into a wake-word engine. Real Sherpa KWS implementations replace <see cref="Audio.StubWakeWordDetector"/>.
/// </summary>
public interface IWakeWordDetector : IDisposable
{
    string DetectorId { get; }

    bool IsRunning { get; }

    event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    void Start();

    void Stop();

    /// <summary>Feed one chunk of capture audio (typically 10–100 ms at 16 kHz mono).</summary>
    void ProcessAudio(ReadOnlySpan<short> pcm16Mono, int sampleRate);

    /// <summary>Update the phrase to listen for (may reload models).</summary>
    void ApplyPhrase(string phrase);

    /// <summary>Adjust streaming AGC + KWS threshold for quiet/loud environments.</summary>
    void ApplyWakeSensitivity(WakeWordSensitivity sensitivity);

    /// <summary>Re-open the KWS stream after post-wake dictation (default: <see cref="Start"/>).</summary>
    void RearmAfterDictation() => Start();

    /// <summary>Emit a periodic listen-path diagnostic snapshot (no-op for test doubles).</summary>
    void FlushPeriodicDiagnostics(WakeWordListenStats listenStats) { }
}
