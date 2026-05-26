using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Test/dev wake-word detector: fires after enough audio chunks or via <see cref="SimulateDetection"/>.
/// Does not load Sherpa KWS models.
/// </summary>
public sealed class StubWakeWordDetector : IWakeWordDetector
{
    public const string DefaultKeyword = "stub-wake";

    private string _keyword;
    private readonly int _chunksBeforeAutoFire;
    private int _chunkCount;
    private bool _running;
    private bool _stubWarned;
    private long _windowChunks;

    public StubWakeWordDetector(
        string keyword = DefaultKeyword,
        int chunksBeforeAutoFire = 0)
    {
        _keyword = string.IsNullOrWhiteSpace(keyword) ? DefaultKeyword : keyword.Trim();
        _chunksBeforeAutoFire = Math.Max(0, chunksBeforeAutoFire);
    }

    public string DetectorId => "stub (no KWS model)";

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

        if (!_stubWarned)
        {
            _stubWarned = true;
            Log.Warning(
                "[WAKE-DIAG] stub detector received audio but cannot recognize wake phrase '{Phrase}' from speech. " +
                "Install models/sherpa-kws to enable real wake-word detection.",
                _keyword);
        }

        _chunkCount++;
        _windowChunks++;
        if (_chunksBeforeAutoFire > 0 && _chunkCount >= _chunksBeforeAutoFire)
        {
            FireDetection();
        }
    }

    public void FlushPeriodicDiagnostics(WakeWordListenStats listen)
    {
        Log.Warning(
            "[WAKE-DIAG] stub detector window: phrase={Phrase} listen={Listening} chunks={Chunks} peakRms={PeakRms:F4}. " +
            "analysis: KWS 模型未加载，无法从真实语音唤醒。",
            _keyword,
            listen.Listening,
            _windowChunks,
            listen.CapturePeakRms);
        _windowChunks = 0;
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

    public void ApplyPhrase(string phrase)
    {
        _keyword = string.IsNullOrWhiteSpace(phrase) ? DefaultKeyword : phrase.Trim();
    }

    public void ApplyWakeSensitivity(WakeWordSensitivity sensitivity)
    {
    }

    public void Dispose()
    {
        Stop();
    }
}
