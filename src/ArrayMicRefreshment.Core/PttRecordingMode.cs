namespace ArrayMicRefreshment.Core;

/// <summary>How the global PTT hotkey starts and stops capture.</summary>
public enum PttRecordingMode
{
    /// <summary>Press and hold to record; release to stop (default).</summary>
    Hold,

    /// <summary>Press once to start; press again to stop.</summary>
    Toggle,
}
