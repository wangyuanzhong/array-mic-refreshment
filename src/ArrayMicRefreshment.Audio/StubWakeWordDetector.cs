using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Test/dev wake-word detector: fires after enough audio chunks or via <see cref="SimulateDetection"/>.
/// Does not load Sherpa KWS models.
/// </summary>
public sealed class StubWakeWordDetector : IWakeWordDetector
{
    public const string DefaultKeyword = "stub-wake";

    private readonly string _keyword;
    private readonly int _chunksBeforeAutoFire;
    private int _chunkCount;
    private bool _running;

    public StubWakeWordDetector(
        string keyword = DefaultKeyword,
        int chunksBeforeAutoFire = 0)
    {
        _keyword = keyword;
        _chunksBeforeAutoFire = Math.Max(0, chunksBeforeAutoFire);
    }

    public string DetectorId => "stub";

    public bool IsRunning => _running;

    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    public void Start()
    {
        _running = true;
        _chunkCount = 0;
    }

    public void Stop()
    {
        _running = false;
        _chunkCount = 0;
    }

    public void ProcessAudio(ReadOnlySpan<short> pcm16Mono, int sampleRate)
    {
        if (!_running || pcm16Mono.Length == 0)
        {
            return;
        }

        _chunkCount++;
        if (_chunksBeforeAutoFire > 0 && _chunkCount >= _chunksBeforeAutoFire)
        {
            FireDetection();
        }
    }

    /// <summary>Manual trigger for integration tests and tray dev actions.</summary>
    public void SimulateDetection(string? keyword = null)
    {
        if (!_running)
        {
            return;
        }

        WakeWordDetected?.Invoke(
            this,
            new WakeWordDetectedEventArgs(keyword ?? _keyword, DateTimeOffset.UtcNow));
    }

    private void FireDetection()
    {
        _chunkCount = 0;
        WakeWordDetected?.Invoke(
            this,
            new WakeWordDetectedEventArgs(_keyword, DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        Stop();
    }
}
