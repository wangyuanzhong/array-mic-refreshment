namespace ArrayMicRefreshment.Core;

/// <summary>Built-in wake-word command capture timing (not exposed in settings).</summary>
public static class WakeWordCaptureDefaults
{
    /// <summary>
    /// Pause after the last speech frame before submitting the wake command.
    /// Tuned for short spoken commands (~0.7s): long enough for a natural breath, short enough to feel responsive.
    /// </summary>
    public static readonly TimeSpan CommandEndSilence = TimeSpan.FromMilliseconds(700);

    public static int CommandEndSilenceMs => (int)CommandEndSilence.TotalMilliseconds;
}
