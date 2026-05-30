namespace ArrayMicRefreshment.Audio;

/// <summary>When to end wake-word command capture after the user stops speaking.</summary>
public static class WakeWordDictationSilence
{
    /// <summary>True when energy stays below the extend threshold continuously for <paramref name="silenceTimeout"/>.</summary>
    public static bool ShouldEndAfterQuietBelowExtend(
        DateTimeOffset now,
        DateTimeOffset? quietBelowExtendSinceUtc,
        TimeSpan silenceTimeout)
    {
        if (!quietBelowExtendSinceUtc.HasValue)
        {
            return false;
        }

        return (now - quietBelowExtendSinceUtc.Value) >= silenceTimeout;
    }

    /// <summary>
    /// Neural VAD path: end after <paramref name="silenceTimeout"/> with no speech frames since <paramref name="lastSpeechUtc"/>.
    /// </summary>
    public static bool ShouldEndAfterVadSilence(
        DateTimeOffset now,
        DateTimeOffset? lastSpeechUtc,
        bool hadSpeech,
        TimeSpan silenceTimeout)
    {
        if (!hadSpeech || !lastSpeechUtc.HasValue)
        {
            return false;
        }

        return (now - lastSpeechUtc.Value) >= silenceTimeout;
    }

    /// <summary>
    /// Energy fallback when VAD model is unavailable: end after silence timeout since last vocal activity (extend threshold).
    /// </summary>
    public static bool ShouldEndAfterVocalActivityGap(
        DateTimeOffset now,
        DateTimeOffset lastVocalActivityUtc,
        bool heardSpeech,
        TimeSpan silenceTimeout)
    {
        if (!heardSpeech)
        {
            return false;
        }

        return (now - lastVocalActivityUtc) >= silenceTimeout;
    }
}
