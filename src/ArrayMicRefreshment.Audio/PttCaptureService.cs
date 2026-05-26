using System.Runtime.InteropServices;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// High-level PTT capture: press accumulates PCM, release finalizes to <see cref="AudioUtterance"/>.
/// PTT release always wins over in-flight VAD segmentation.
/// </summary>
public sealed class PttCaptureService : IDisposable
{
    public const int DefaultRingBufferSeconds = 120;
    public static readonly TimeSpan VadAssistMinHold = TimeSpan.FromSeconds(8);
    private const int StandbyRingCapacityBytes = 576_000;
    private const double StandbyPreRollSeconds = 1.5;
    private const int WasapiCaptureBufferMs = 20;

    private readonly AppSettings _settings;
    private readonly IPushToTalkSource _ptt;
    private readonly IAudioDeviceEnumerator _deviceEnumerator;
    private readonly IAudioCaptureStreamFactory _captureFactory;
    private readonly IVoiceActivityDetector _vad;
    private readonly AudioHostApi _hostApi;
    private readonly TimeSpan _vadAssistMinHold;
    private readonly Func<bool>? _pttCaptureAllowed;
    private readonly Func<bool>? _keepStandbyCaptureBetweenSessions;
    private readonly Func<PttCaptureHandoff?>? _tryCaptureHandoffOnPress;
    private readonly Func<byte[]?>? _takePreRollOnPress;
    private readonly Action? _beforeCaptureStarts;
    private readonly Action? _afterCaptureEnds;
    private readonly ByteRingBuffer _ring;
    private readonly ByteRingBuffer _standbyRing;
    private readonly List<byte> _segmentBuffer = new();
    private readonly object _gate = new();

    private IAudioCaptureStream? _stream;
    private string? _activeDeviceId;
    private bool _pttHeld;
    private bool _drainingCapture;
    private bool _captureRunning;
    private bool _standbyActive;
    private DateTimeOffset _pressUtc;
    private int _releaseCount;
    private System.Threading.Timer? _vadPollTimer;

    public event EventHandler<AudioUtterance>? UtteranceReady;

    public event EventHandler<Exception>? CaptureFailed;

    public event EventHandler<string>? CaptureEmpty;

    public PttCaptureService(
        AppSettings settings,
        IPushToTalkSource ptt,
        IAudioDeviceEnumerator deviceEnumerator,
        IAudioCaptureStreamFactory captureFactory,
        IVoiceActivityDetector? vad = null,
        AudioHostApi hostApi = AudioHostApi.Wasapi,
        int ringBufferSeconds = DefaultRingBufferSeconds,
        TimeSpan? vadAssistMinHold = null,
        Func<bool>? pttCaptureAllowed = null,
        Func<bool>? keepStandbyCaptureBetweenSessions = null,
        Func<PttCaptureHandoff?>? tryCaptureHandoffOnPress = null,
        Func<byte[]?>? takePreRollOnPress = null,
        Action? beforeCaptureStarts = null,
        Action? afterCaptureEnds = null)
    {
        _settings = settings;
        _ptt = ptt;
        _deviceEnumerator = deviceEnumerator;
        _captureFactory = captureFactory;
        _vad = vad ?? new NullVoiceActivityDetector();
        _hostApi = hostApi;
        _vadAssistMinHold = vadAssistMinHold ?? VadAssistMinHold;
        _pttCaptureAllowed = pttCaptureAllowed;
        _keepStandbyCaptureBetweenSessions = keepStandbyCaptureBetweenSessions;
        _tryCaptureHandoffOnPress = tryCaptureHandoffOnPress;
        _takePreRollOnPress = takePreRollOnPress;
        _beforeCaptureStarts = beforeCaptureStarts;
        _afterCaptureEnds = afterCaptureEnds;
        _ring = new ByteRingBuffer(Math.Max(16000 * 2 * ringBufferSeconds, 32000));
        _standbyRing = new ByteRingBuffer(StandbyRingCapacityBytes);

        _ptt.PttPressed += OnPttPressed;
        _ptt.PttReleased += OnPttReleased;
    }

    public int ReleaseEventCount => _releaseCount;

    public bool IsPttHeld
    {
        get
        {
            lock (_gate)
            {
                return _pttHeld;
            }
        }
    }

    public void SimulateReleaseForDev()
    {
        if (_ptt is StubPushToTalkSource stub)
        {
            stub.SimulateRelease();
            return;
        }

        OnPttReleased(_ptt, EventArgs.Empty);
    }

    private void OnPttPressed(object? sender, EventArgs e)
    {
        if (_pttCaptureAllowed is not null && !_pttCaptureAllowed())
        {
            Serilog.Log.Debug("PTT press ignored (capture not allowed for current trigger mode)");
            return;
        }

        lock (_gate)
        {
            if (_pttHeld)
            {
                return;
            }

            _pttHeld = true;
            _pressUtc = DateTimeOffset.UtcNow;
            _segmentBuffer.Clear();
            _ring.Clear();
            _vad.Reset();

            try
            {
                var handoff = _tryCaptureHandoffOnPress?.Invoke();
                byte[]? wakePreRoll = null;
                if (handoff is null)
                {
                    wakePreRoll = _takePreRollOnPress?.Invoke();
                }

                if (handoff is not null)
                {
                    AdoptCaptureHandoff(handoff);
                }
                else
                {
                    var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId)
                        ?? throw new InvalidOperationException("No capture device available.");

                    EnsureCaptureStream(device);
                    if (!_captureRunning)
                    {
                        _stream!.Start();
                        _captureRunning = true;
                    }

                    if (_standbyActive)
                    {
                        var standbyPreRoll = _standbyRing.SnapshotLast(StandbyPreRollBytesForStream());
                        if (standbyPreRoll.Length > 0)
                        {
                            _segmentBuffer.AddRange(standbyPreRoll);
                            _ring.Write(standbyPreRoll);
                            Serilog.Log.Information(
                                "PTT standby pre-roll injected ({Bytes} bytes)",
                                standbyPreRoll.Length);
                        }
                    }
                    else if (ShouldKeepStandbyCapture())
                    {
                        Serilog.Log.Warning(
                            "PTT pressed without standby pre-roll; first ~{Ms}ms of speech may be clipped. " +
                            "Standby capture will start for subsequent presses.",
                            WasapiCaptureBufferMs);
                        StartStandbyListeningIfNeeded();
                    }

                    if (wakePreRoll is { Length: > 0 })
                    {
                        _segmentBuffer.AddRange(wakePreRoll);
                        _ring.Write(wakePreRoll);
                        Serilog.Log.Information(
                            "PTT pre-roll injected from wake listen path ({Bytes} bytes)",
                            wakePreRoll.Length);
                    }

                    _beforeCaptureStarts?.Invoke();
                }

                StartVadPolling();
                Serilog.Log.Information(
                    "PTT capture ready {Ms}ms after press (standby={Standby}, segment={Bytes} bytes)",
                    (int)(DateTimeOffset.UtcNow - _pressUtc).TotalMilliseconds,
                    _standbyActive,
                    _segmentBuffer.Count);
            }
            catch (Exception ex)
            {
                _pttHeld = false;
                StopCaptureOnly();
                CaptureFailed?.Invoke(this, ex);
            }
        }
    }

    private void AdoptCaptureHandoff(PttCaptureHandoff handoff)
    {
        if (_stream is not null && !ReferenceEquals(_stream, handoff.Stream))
        {
            StopStream();
        }

        _stream = handoff.Stream;
        _activeDeviceId = handoff.DeviceId;
        _stream.DataAvailable -= OnCaptureData;
        _stream.DataAvailable += OnCaptureData;
        _captureRunning = true;
        _standbyActive = false;
        _standbyRing.Clear();

        if (handoff.PreRollNativePcm.Length > 0)
        {
            _segmentBuffer.AddRange(handoff.PreRollNativePcm);
            _ring.Write(handoff.PreRollNativePcm);
        }

        Serilog.Log.Information(
            "PTT adopted wake listen stream handoff (preRoll={PreRollBytes} bytes, device={Device}, running={Running})",
            handoff.PreRollNativePcm.Length,
            handoff.DeviceId,
            _captureRunning);
    }

    private void OnPttReleased(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!_pttHeld)
            {
                return;
            }

            _releaseCount++;
            StopVadPolling();

            _drainingCapture = true;
            _pttHeld = false;
            try
            {
                var keepStandby = ShouldKeepStandbyCapture();
                if (!keepStandby)
                {
                    StopCaptureOnly();
                    _standbyActive = false;
                    _standbyRing.Clear();
                }

                var utterance = FinalizeUtterance();
                if (utterance is not null)
                {
                    UtteranceReady?.Invoke(this, utterance);
                }
                else
                {
                    CaptureEmpty?.Invoke(this, "未录到音频数据。请检查录音设备是否选对、麦克风是否静音。");
                }
            }
            finally
            {
                _drainingCapture = false;
                _afterCaptureEnds?.Invoke();
            }
        }
    }

    private void OnCaptureData(object? sender, ReadOnlyMemory<byte> chunk)
    {
        lock (_gate)
        {
            if (_pttHeld || _drainingCapture)
            {
                _ring.Write(chunk.Span);
                _segmentBuffer.AddRange(chunk.Span);
                return;
            }

            if (_standbyActive)
            {
                _standbyRing.Write(chunk.Span);
            }
        }
    }

    private void StartVadPolling()
    {
        _vadPollTimer?.Dispose();
        _vadPollTimer = new System.Threading.Timer(_ => PollVad(), null, 200, 200);
    }

    private void StopVadPolling()
    {
        _vadPollTimer?.Dispose();
        _vadPollTimer = null;
    }

    private void PollVad()
    {
        lock (_gate)
        {
            if (!_pttHeld)
            {
                return;
            }

            if (DateTimeOffset.UtcNow - _pressUtc < _vadAssistMinHold)
            {
                return;
            }

            if (_stream is null || _segmentBuffer.Count < 3200)
            {
                return;
            }

            var tail = _segmentBuffer.Count > 8000
                ? _segmentBuffer.ToArray()[^8000..]
                : _segmentBuffer.ToArray();
            var mono16k = PcmResampler.To16kHzMono16Le(
                tail,
                _stream.SampleRate,
                _stream.Channels,
                _stream.BitsPerSample);
            var samples = MemoryMarshal.Cast<byte, short>(mono16k.AsSpan());
            _ = _vad.IsEndOfSpeech(samples, PcmResampler.TargetSampleRate);
        }
    }

    private AudioUtterance? FinalizeUtterance()
    {
        if (_stream is null || _segmentBuffer.Count == 0)
        {
            return null;
        }

        var pcm = _segmentBuffer.ToArray();
        var resampled = PcmResampler.To16kHzMono16Le(
            pcm,
            _stream.SampleRate,
            _stream.Channels,
            _stream.BitsPerSample);
        var duration = TimeSpan.FromSeconds((double)resampled.Length / (2 * PcmResampler.TargetSampleRate));
        return new AudioUtterance
        {
            Pcm16LeMono = resampled,
            SampleRate = PcmResampler.TargetSampleRate,
            Duration = duration,
        };
    }

    /// <summary>Keep WASAPI open and record a rolling pre-roll so PTT press is instant.</summary>
    public void WarmCaptureDevice() => StartStandbyListeningIfNeeded();

    public void StartStandbyListeningIfNeeded()
    {
        if (!ShouldKeepStandbyCapture())
        {
            return;
        }

        if (_pttCaptureAllowed is not null && !_pttCaptureAllowed())
        {
            return;
        }

        lock (_gate)
        {
            if (_pttHeld || _captureRunning)
            {
                return;
            }

            try
            {
                var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId);
                if (device is null)
                {
                    return;
                }

                EnsureCaptureStream(device);
                _stream!.Start();
                _captureRunning = true;
                _standbyActive = true;
                Serilog.Log.Information(
                    "PTT standby capture started (device={Device})",
                    device.DisplayName);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "PTT standby capture failed");
                StopStream();
            }
        }
    }

    public void StopStandbyListening()
    {
        lock (_gate)
        {
            if (_pttHeld)
            {
                return;
            }

            _standbyActive = false;
            _standbyRing.Clear();
            StopStream();
        }
    }

    public void InvalidateCaptureDevice()
    {
        lock (_gate)
        {
            if (_pttHeld)
            {
                return;
            }

            StopStream();
            _standbyActive = false;
            _standbyRing.Clear();
        }
    }

    private bool ShouldKeepStandbyCapture() =>
        _keepStandbyCaptureBetweenSessions?.Invoke() == true;

    private int StandbyPreRollBytesForStream()
    {
        if (_stream is null)
        {
            return 96_000;
        }

        var bytesPerSecond = _stream.SampleRate * _stream.Channels * (_stream.BitsPerSample / 8);
        return Math.Max(3200, (int)(bytesPerSecond * StandbyPreRollSeconds));
    }

    private void EnsureCaptureStream(AudioDeviceInfo device)
    {
        if (_stream is not null && _activeDeviceId == device.Id)
        {
            return;
        }

        StopStream();
        _activeDeviceId = device.Id;
        _stream = _captureFactory.Open(device);
        _stream.DataAvailable += OnCaptureData;
        _captureRunning = false;
        _standbyActive = false;
        _standbyRing.Clear();
    }

    private void StopCaptureOnly()
    {
        if (_stream is null || !_captureRunning)
        {
            return;
        }

        _stream.Stop();
        _captureRunning = false;
    }

    private void StopStream()
    {
        StopCaptureOnly();
        if (_stream is null)
        {
            return;
        }

        _stream.DataAvailable -= OnCaptureData;
        _stream.Dispose();
        _stream = null;
        _activeDeviceId = null;
    }

    public void Dispose()
    {
        _ptt.PttPressed -= OnPttPressed;
        _ptt.PttReleased -= OnPttReleased;
        StopVadPolling();
        lock (_gate)
        {
            StopStream();
        }
    }
}
