using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

#if WINDOWS
/// <summary>Records PCM from the configured capture device for speaker enrollment.</summary>
public sealed class EnrollmentUtteranceCapture : IEnrollmentUtteranceSource, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IAudioDeviceEnumerator _deviceEnumerator;
    private readonly IAudioCaptureStreamFactory _captureFactory;

    private IAudioCaptureStream? _stream;
    private readonly List<byte> _buffer = new();
    private DateTimeOffset _startedUtc;

    public EnrollmentUtteranceCapture(
        AppSettings settings,
        IAudioDeviceEnumerator deviceEnumerator,
        IAudioCaptureStreamFactory captureFactory)
    {
        _settings = settings;
        _deviceEnumerator = deviceEnumerator;
        _captureFactory = captureFactory;
    }

    public EnrollmentRecordingSession StartRecording()
    {
        StopInternal();

        var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId);
        if (device is null)
        {
            throw new InvalidOperationException("No capture device available for enrollment.");
        }

        _buffer.Clear();
        _stream = _captureFactory.Open(device);
        _stream.DataAvailable += OnData;
        _stream.Start();
        _startedUtc = DateTimeOffset.UtcNow;

        return new EnrollmentRecordingSession(StopAndBuild, StopInternal);
    }

    private void OnData(object? sender, ReadOnlyMemory<byte> data)
    {
        _buffer.AddRange(data.Span);
    }

    private AudioUtterance? StopAndBuild()
    {
        if (_stream is null)
        {
            return null;
        }

        var duration = DateTimeOffset.UtcNow - _startedUtc;
        var sampleRate = _stream.SampleRate;
        var channels = _stream.Channels;
        StopInternal();

        if (_buffer.Count == 0)
        {
            return null;
        }

        var pcm16kMono = PcmResampler.To16kHzMono16Le(_buffer.ToArray(), sampleRate, channels);
        if (pcm16kMono.Length == 0)
        {
            return null;
        }

        return new AudioUtterance
        {
            Pcm16LeMono = pcm16kMono,
            SampleRate = PcmResampler.TargetSampleRate,
            Duration = duration,
        };
    }

    private void StopInternal()
    {
        if (_stream is null)
        {
            return;
        }

        _stream.DataAvailable -= OnData;
        _stream.Stop();
        _stream.Dispose();
        _stream = null;
    }

    public void Dispose() => StopInternal();
}
#endif
