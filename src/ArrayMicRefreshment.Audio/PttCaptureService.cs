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

    private readonly AppSettings _settings;
    private readonly IPushToTalkSource _ptt;
    private readonly IAudioDeviceEnumerator _deviceEnumerator;
    private readonly IAudioCaptureStreamFactory _captureFactory;
    private readonly IVoiceActivityDetector _vad;
    private readonly AudioHostApi _hostApi;
    private readonly TimeSpan _vadAssistMinHold;
    private readonly ByteRingBuffer _ring;
    private readonly List<byte> _segmentBuffer = new();
    private readonly object _gate = new();

    private IAudioCaptureStream? _stream;
    private bool _pttHeld;
    private DateTimeOffset _pressUtc;
    private int _releaseCount;
    private System.Threading.Timer? _vadPollTimer;

    public event EventHandler<AudioUtterance>? UtteranceReady;

    public PttCaptureService(
        AppSettings settings,
        IPushToTalkSource ptt,
        IAudioDeviceEnumerator deviceEnumerator,
        IAudioCaptureStreamFactory captureFactory,
        IVoiceActivityDetector? vad = null,
        AudioHostApi hostApi = AudioHostApi.Wasapi,
        int ringBufferSeconds = DefaultRingBufferSeconds,
        TimeSpan? vadAssistMinHold = null)
    {
        _settings = settings;
        _ptt = ptt;
        _deviceEnumerator = deviceEnumerator;
        _captureFactory = captureFactory;
        _vad = vad ?? new NullVoiceActivityDetector();
        _hostApi = hostApi;
        _vadAssistMinHold = vadAssistMinHold ?? VadAssistMinHold;
        _ring = new ByteRingBuffer(Math.Max(16000 * 2 * ringBufferSeconds, 32000));

        _ptt.PttPressed += OnPttPressed;
        _ptt.PttReleased += OnPttReleased;
    }

    public int ReleaseEventCount => _releaseCount;

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

            StopStream();
            try
            {
                var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId)
                    ?? throw new InvalidOperationException("No capture device available.");

                _stream = _captureFactory.Open(device);
                _stream.DataAvailable += OnCaptureData;
                _stream.Start();
                StartVadPolling();
            }
            catch
            {
                _pttHeld = false;
                StopStream();
                throw;
            }
        }
    }

    private void OnPttReleased(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!_pttHeld)
            {
                return;
            }

            _pttHeld = false;
            _releaseCount++;
            StopVadPolling();

            var utterance = FinalizeUtterance();
            StopStream();
            if (utterance is not null)
            {
                UtteranceReady?.Invoke(this, utterance);
            }
        }
    }

    private void OnCaptureData(object? sender, ReadOnlyMemory<byte> chunk)
    {
        lock (_gate)
        {
            if (!_pttHeld)
            {
                return;
            }

            _ring.Write(chunk.Span);
            _segmentBuffer.AddRange(chunk.Span);
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

    private void StopStream()
    {
        if (_stream is null)
        {
            return;
        }

        _stream.DataAvailable -= OnCaptureData;
        _stream.Stop();
        _stream.Dispose();
        _stream = null;
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
