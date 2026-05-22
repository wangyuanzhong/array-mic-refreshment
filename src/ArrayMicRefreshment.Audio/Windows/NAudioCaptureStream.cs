#if WINDOWS
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ArrayMicRefreshment.Audio;

public sealed class NAudioCaptureStreamFactory : IAudioCaptureStreamFactory
{
    public IAudioCaptureStream Open(AudioDeviceInfo device) => new NAudioCaptureStream(device);
}

public sealed class NAudioCaptureStream : IAudioCaptureStream
{
    private readonly AudioDeviceInfo _device;
    private IWaveIn? _waveIn;
    private bool _started;

    public NAudioCaptureStream(AudioDeviceInfo device)
    {
        _device = device;
        SampleRate = device.DefaultSampleRate > 0 ? device.DefaultSampleRate : 48000;
        Channels = Math.Max(1, device.Channels);
        BitsPerSample = 16;
    }

    public int SampleRate { get; private set; }
    public int Channels { get; }
    public int BitsPerSample { get; }

    public event EventHandler<ReadOnlyMemory<byte>>? DataAvailable;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _waveIn = CreateWaveIn();
        _waveIn.DataAvailable += (_, e) =>
        {
            var buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
            DataAvailable?.Invoke(this, buffer);
        };
        _waveIn.RecordingStopped += (_, _) => { };
        SampleRate = _waveIn.WaveFormat.SampleRate;
        _waveIn.StartRecording();
        _started = true;
    }

    public void Stop()
    {
        if (!_started || _waveIn is null)
        {
            return;
        }

        _waveIn.StopRecording();
        _started = false;
    }

    public void Dispose()
    {
        Stop();
        _waveIn?.Dispose();
        _waveIn = null;
    }

    private IWaveIn CreateWaveIn()
    {
        if (_device.HostApi == AudioHostApi.Wasapi && _device.Id.StartsWith("wasapi:", StringComparison.Ordinal))
        {
            var mmId = _device.Id["wasapi:".Length..];
            using var enumerator = new MMDeviceEnumerator();
            var mm = enumerator.GetDevice(mmId);
            var capture = new WasapiCapture(mm)
            {
                ShareMode = AudioClientShareMode.Shared,
            };
            return capture;
        }

        var index = ParseIndex(_device.Id);
        return new WaveInEvent
        {
            DeviceNumber = index,
            WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
            BufferMilliseconds = 50,
        };
    }

    private static int ParseIndex(string id)
    {
        var colon = id.LastIndexOf(':');
        if (colon >= 0 && int.TryParse(id[(colon + 1)..], out var idx))
        {
            return idx;
        }

        return 0;
    }
}
#endif
