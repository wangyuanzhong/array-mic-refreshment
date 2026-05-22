using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

/// <summary>Captures a single enrollment utterance (injected; no Sherpa calls).</summary>
public interface IEnrollmentUtteranceSource
{
    EnrollmentRecordingSession StartRecording();
}

public sealed class EnrollmentRecordingSession : IDisposable
{
    private readonly Func<AudioUtterance?> _stop;
    private readonly Action? _onDispose;

    public EnrollmentRecordingSession(Func<AudioUtterance?> stop, Action? onDispose = null)
    {
        _stop = stop;
        _onDispose = onDispose;
    }

    public AudioUtterance? Stop() => _stop();

    public void Dispose() => _onDispose?.Invoke();
}
