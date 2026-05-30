namespace ArrayMicRefreshment.Audio;

/// <summary>When to end wake-word command capture after the user stops speaking.</summary>
public static class WakeWordDictationSilence
{
    /// <summary>True when no vocal activity (incl. soft syllables) for at least <paramref name="silenceTimeout"/>.</summary>
    public static bool ShouldEnd(
        DateTimeOffset now,
        DateTimeOffset lastVocalActivityUtc,
        TimeSpan silenceTimeout)
        => (now - lastVocalActivityUtc) >= silenceTimeout;
}
