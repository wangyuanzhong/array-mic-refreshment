namespace ArrayMicRefreshment.Audio;

/// <summary>Optional VAD assist (Silero via Sherpa-ONNX) for PTT long-hold and wake dictation end.</summary>
public interface IVoiceActivityDetector
{
    /// <summary>True when a Silero (or other neural) model is loaded — not energy-only fallback.</summary>
    bool IsAvailable { get; }

    /// <summary>Speech observed since the last <see cref="Reset"/>.</summary>
    bool HadSpeechSinceReset { get; }

    /// <summary>UTC of last frame classified as speech; null if none yet.</summary>
    DateTimeOffset? LastSpeechActivityUtc { get; }

    /// <summary>Feed mono PCM and update internal state. True when silence after speech exceeds configured duration.</summary>
    bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate);

    void ConfigureSilenceDuration(TimeSpan silence);

    void Reset();
}

public sealed class NullVoiceActivityDetector : IVoiceActivityDetector
{
    public bool IsAvailable => false;

    public bool HadSpeechSinceReset => false;

    public DateTimeOffset? LastSpeechActivityUtc => null;

    public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate) => false;

    public void ConfigureSilenceDuration(TimeSpan silence)
    {
    }

    public void Reset()
    {
    }
}
