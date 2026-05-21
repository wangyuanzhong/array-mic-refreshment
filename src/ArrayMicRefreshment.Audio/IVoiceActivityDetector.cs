namespace ArrayMicRefreshment.Audio;

/// <summary>Optional VAD assist while PTT is held (Silero or test doubles).</summary>
public interface IVoiceActivityDetector
{
    /// <summary>True when the chunk looks like end-of-speech (silence tail).</summary>
    bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate);

    void Reset();
}

public sealed class NullVoiceActivityDetector : IVoiceActivityDetector
{
    public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate) => false;

    public void Reset()
    {
    }
}
