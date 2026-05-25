using System.Runtime.InteropServices;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Wake-word path: continuous listen → dictation session → <see cref="UtteranceCaptureEventArgs"/>.
/// Continuous KWS and session VAD are skeletal; dictation capture/finalize mirrors <see cref="PttCaptureService"/>.
/// </summary>
public sealed class WakeWordCaptureService : IWakeWordCaptureService
{
    public static readonly TimeSpan DefaultPostWakeSilenceTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan DefaultPostWakeMaxSession = TimeSpan.FromSeconds(60);

    private readonly AppSettings _settings;
    private readonly IWakeWordDetector _detector;
    private readonly IAudioDeviceEnumerator _deviceEnumerator;
    private readonly IAudioCaptureStreamFactory _captureFactory;
    private readonly IVoiceActivityDetector _vad;
    private readonly TimeSpan _silenceTimeout;
    private readonly TimeSpan _maxSession;
    private readonly object _gate = new();

    private IAudioCaptureStream? _listenStream;
    private IAudioCaptureStream? _dictationStream;
    private string? _activeDeviceId;
    private bool _listening;
    private bool _dictationActive;
    private readonly List<byte> _dictationBuffer = new();
    private DateTimeOffset _dictationStartedUtc;
    private DateTimeOffset _lastSpeechUtc;
    private DateTimeOffset? _vadEndCandidateUtc;
    private System.Threading.Timer? _sessionTimer;

    public WakeWordCaptureService(
        AppSettings settings,
        IWakeWordDetector detector,
        IAudioDeviceEnumerator deviceEnumerator,
        IAudioCaptureStreamFactory captureFactory,
        IVoiceActivityDetector? vad = null,
        TimeSpan? postWakeSilenceTimeout = null,
        TimeSpan? postWakeMaxSession = null)
    {
        _settings = settings;
        _detector = detector;
        _deviceEnumerator = deviceEnumerator;
        _captureFactory = captureFactory;
        _vad = vad ?? new NullVoiceActivityDetector();
        _silenceTimeout = postWakeSilenceTimeout ?? DefaultPostWakeSilenceTimeout;
        _maxSession = postWakeMaxSession ?? DefaultPostWakeMaxSession;

        _detector.WakeWordDetected += OnWakeWordDetected;
    }

    public event EventHandler<UtteranceCaptureEventArgs>? UtteranceReady;

    public event EventHandler<Exception>? CaptureFailed;

    public event EventHandler<string>? CaptureEmpty;

    public event EventHandler<string>? StatusChanged;

    public bool IsListening
    {
        get
        {
            lock (_gate)
            {
                return _listening;
            }
        }
    }

    public bool IsDictationActive
    {
        get
        {
            lock (_gate)
            {
                return _dictationActive;
            }
        }
    }

    public void StartListening()
    {
        lock (_gate)
        {
            if (_listening)
            {
                return;
            }

            try
            {
                var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId)
                    ?? throw new InvalidOperationException("No capture device available.");

                EnsureListenStream(device);
                _listenStream!.Start();
                _detector.Start();
                _listening = true;
                RaiseStatus("监听唤醒词…");
                Log.Information(
                    "Wake-word listening started (detector={DetectorId}, device={Device})",
                    _detector.DetectorId,
                    device.DisplayName);
            }
            catch (Exception ex)
            {
                StopListeningInternal();
                CaptureFailed?.Invoke(this, ex);
                Log.Warning(ex, "Failed to start wake-word listening");
            }
        }
    }

    public void StopListening()
    {
        lock (_gate)
        {
            StopListeningInternal();
        }
    }

    private void StopListeningInternal()
    {
        _detector.Stop();
        StopListenStream();
        CancelDictationSession();
        _listening = false;
        RaiseStatus("唤醒监听已停止");
        Log.Information("Wake-word listening stopped");
    }

    private void OnWakeWordDetected(object? sender, WakeWordDetectedEventArgs e)
    {
        lock (_gate)
        {
            if (!_listening || _dictationActive)
            {
                return;
            }

            Log.Information(
                "Wake word detected: {Keyword} at {Utc}",
                e.Keyword,
                e.DetectedAtUtc);

            try
            {
                BeginDictationSession();
            }
            catch (Exception ex)
            {
                CaptureFailed?.Invoke(this, ex);
                Log.Warning(ex, "Failed to start post-wake dictation");
            }
        }
    }

    private void BeginDictationSession()
    {
        var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId)
            ?? throw new InvalidOperationException("No capture device available.");

        StopListenStream();
        _detector.Stop();

        EnsureDictationStream(device);
        _dictationBuffer.Clear();
        _vad.Reset();
        _dictationStartedUtc = DateTimeOffset.UtcNow;
        _lastSpeechUtc = _dictationStartedUtc;
        _vadEndCandidateUtc = null;
        _dictationActive = true;
        _dictationStream!.Start();

        _sessionTimer?.Dispose();
        _sessionTimer = new System.Threading.Timer(
            _ => PollDictationSession(),
            null,
            200,
            200);

        RaiseStatus("已唤醒，请说话…");
        Log.Information("Post-wake dictation session started");
    }

    private void PollDictationSession()
    {
        lock (_gate)
        {
            if (!_dictationActive || _dictationStream is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _dictationStartedUtc >= _maxSession)
            {
                Log.Information("Wake dictation max session reached ({Seconds}s)", _maxSession.TotalSeconds);
                EndDictationSession();
                return;
            }

            if (_dictationBuffer.Count < 3200)
            {
                if (now - _dictationStartedUtc >= _silenceTimeout)
                {
                    Log.Information("Wake dictation silence timeout with no audio");
                    EndDictationSession(empty: true);
                }

                return;
            }

            var tail = _dictationBuffer.Count > 8000
                ? _dictationBuffer.ToArray()[^8000..]
                : _dictationBuffer.ToArray();
            var mono16k = PcmResampler.To16kHzMono16Le(
                tail,
                _dictationStream.SampleRate,
                _dictationStream.Channels,
                _dictationStream.BitsPerSample);
            var samples = MemoryMarshal.Cast<byte, short>(mono16k.AsSpan());
            var vadDetectedEnd = _vad.IsEndOfSpeech(samples, PcmResampler.TargetSampleRate);
            if (vadDetectedEnd)
            {
                _vadEndCandidateUtc ??= now;
                if (now - _vadEndCandidateUtc >= _silenceTimeout)
                {
                    Log.Information(
                        "Wake dictation VAD end confirmed after {SilenceSeconds}s",
                        _silenceTimeout.TotalSeconds);
                    EndDictationSession();
                    return;
                }
            }
            else
            {
                _vadEndCandidateUtc = null;
            }

            var rms = ComputeRms16Le(mono16k);
            if (rms >= 0.003)
            {
                _lastSpeechUtc = now;
            }
            else
            {
                if (now - _lastSpeechUtc >= _silenceTimeout)
                {
                    Log.Information("Wake dictation silence timeout after speech ({SilenceSeconds}s)", _silenceTimeout.TotalSeconds);
                    EndDictationSession();
                }
            }
        }
    }

    private void EndDictationSession(bool empty = false)
    {
        if (!_dictationActive)
        {
            return;
        }

        _sessionTimer?.Dispose();
        _sessionTimer = null;
        _dictationActive = false;
        _vadEndCandidateUtc = null;

        if (_dictationStream is not null)
        {
            _dictationStream.Stop();
        }

        AudioUtterance? utterance = null;
        if (!empty && _dictationStream is not null && _dictationBuffer.Count > 0)
        {
            utterance = FinalizeUtterance();
        }

        StopDictationStream();

        if (utterance is not null)
        {
            UtteranceReady?.Invoke(
                this,
                new UtteranceCaptureEventArgs(utterance, VoiceTriggerKind.WakeWord));
            RaiseStatus("唤醒句段已提交识别");
        }
        else
        {
            CaptureEmpty?.Invoke(this, "唤醒后未录到有效语音，请重试。");
            RaiseStatus("唤醒后无有效语音");
        }

        if (_listening)
        {
            try
            {
                var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId);
                if (device is not null)
                {
                    EnsureListenStream(device);
                    _listenStream!.Start();
                    _detector.Start();
                    RaiseStatus("监听唤醒词…");
                }
            }
            catch (Exception ex)
            {
                _listening = false;
                CaptureFailed?.Invoke(this, ex);
            }
        }
    }

    private void CancelDictationSession()
    {
        if (!_dictationActive)
        {
            return;
        }

        _sessionTimer?.Dispose();
        _sessionTimer = null;
        _dictationActive = false;
        _vadEndCandidateUtc = null;
        _dictationBuffer.Clear();
        StopDictationStream();
    }

    private void OnListenData(object? sender, ReadOnlyMemory<byte> chunk)
    {
        lock (_gate)
        {
            if (!_listening || _dictationActive)
            {
                return;
            }

            if (_listenStream is null)
            {
                return;
            }

            var mono16k = PcmResampler.To16kHzMono16Le(
                chunk.Span,
                _listenStream.SampleRate,
                _listenStream.Channels,
                _listenStream.BitsPerSample);
            var samples = MemoryMarshal.Cast<byte, short>(mono16k.AsSpan());
            _detector.ProcessAudio(samples, PcmResampler.TargetSampleRate);
        }
    }

    private void OnDictationData(object? sender, ReadOnlyMemory<byte> chunk)
    {
        lock (_gate)
        {
            if (!_dictationActive)
            {
                return;
            }

            _dictationBuffer.AddRange(chunk.Span);
        }
    }

    private AudioUtterance? FinalizeUtterance()
    {
        if (_dictationStream is null || _dictationBuffer.Count == 0)
        {
            return null;
        }

        var pcm = _dictationBuffer.ToArray();
        var resampled = PcmResampler.To16kHzMono16Le(
            pcm,
            _dictationStream.SampleRate,
            _dictationStream.Channels,
            _dictationStream.BitsPerSample);
        var duration = TimeSpan.FromSeconds((double)resampled.Length / (2 * PcmResampler.TargetSampleRate));
        return new AudioUtterance
        {
            Pcm16LeMono = resampled,
            SampleRate = PcmResampler.TargetSampleRate,
            Duration = duration,
        };
    }

    private void EnsureListenStream(AudioDeviceInfo device)
    {
        if (_listenStream is not null && _activeDeviceId == device.Id)
        {
            return;
        }

        StopListenStream();
        _activeDeviceId = device.Id;
        _listenStream = _captureFactory.Open(device);
        _listenStream.DataAvailable += OnListenData;
    }

    private void EnsureDictationStream(AudioDeviceInfo device)
    {
        if (_dictationStream is not null && _activeDeviceId == device.Id)
        {
            return;
        }

        StopDictationStream();
        _activeDeviceId = device.Id;
        _dictationStream = _captureFactory.Open(device);
        _dictationStream.DataAvailable += OnDictationData;
    }

    private void StopListenStream()
    {
        if (_listenStream is null)
        {
            return;
        }

        try
        {
            _listenStream.Stop();
        }
        catch
        {
            // ignore stop races
        }

        _listenStream.DataAvailable -= OnListenData;
        _listenStream.Dispose();
        _listenStream = null;
    }

    private void StopDictationStream()
    {
        if (_dictationStream is null)
        {
            return;
        }

        try
        {
            _dictationStream.Stop();
        }
        catch
        {
            // ignore
        }

        _dictationStream.DataAvailable -= OnDictationData;
        _dictationStream.Dispose();
        _dictationStream = null;
    }

    private void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);

    private static double ComputeRms16Le(byte[] pcm)
    {
        if (pcm.Length < 2)
        {
            return 0;
        }

        var samples = MemoryMarshal.Cast<byte, short>(pcm.AsSpan());
        double sum = 0;
        foreach (var s in samples)
        {
            var n = s / 32768.0;
            sum += n * n;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    public void Dispose()
    {
        _detector.WakeWordDetected -= OnWakeWordDetected;
        lock (_gate)
        {
            StopListeningInternal();
        }

        _detector.Dispose();
    }
}
