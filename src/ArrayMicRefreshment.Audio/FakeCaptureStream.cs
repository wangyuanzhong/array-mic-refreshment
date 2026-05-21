namespace ArrayMicRefreshment.Audio;

/// <summary>In-memory capture stream for cross-platform unit tests.</summary>
public sealed class FakeCaptureStream : IAudioCaptureStream
{
    private readonly byte[] _payload;
    private readonly int _chunkSize;
    private System.Threading.Timer? _timer;
    private int _offset;

    public FakeCaptureStream(
        byte[] payload,
        int sampleRate,
        int channels,
        int bitsPerSample = 16,
        int chunkSize = 3200)
    {
        _payload = payload;
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
        _chunkSize = chunkSize;
    }

    public int SampleRate { get; }
    public int Channels { get; }
    public int BitsPerSample { get; }

    public event EventHandler<ReadOnlyMemory<byte>>? DataAvailable;

    public void Start()
    {
        _offset = 0;
        _timer?.Dispose();
        _timer = new System.Threading.Timer(_ => Pump(), null, 0, 20);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Pump()
    {
        if (_offset >= _payload.Length)
        {
            return;
        }

        var len = Math.Min(_chunkSize, _payload.Length - _offset);
        var chunk = _payload.AsMemory(_offset, len);
        _offset += len;
        DataAvailable?.Invoke(this, chunk);
    }

    public void Dispose()
    {
        Stop();
    }
}

public sealed class FakeCaptureStreamFactory : IAudioCaptureStreamFactory
{
    private readonly Func<AudioDeviceInfo, FakeCaptureStream> _factory;

    public FakeCaptureStreamFactory(Func<AudioDeviceInfo, FakeCaptureStream> factory)
    {
        _factory = factory;
    }

    public IAudioCaptureStream Open(AudioDeviceInfo device) => _factory(device);
}

public sealed class FakeAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    private readonly IReadOnlyList<AudioDeviceInfo> _devices;

    public FakeAudioDeviceEnumerator(params AudioDeviceInfo[] devices)
    {
        _devices = devices;
    }

    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices() => _devices;

    public AudioDeviceInfo? ResolveDevice(string? selectedDeviceId)
    {
        if (string.IsNullOrEmpty(selectedDeviceId))
        {
            return _devices.FirstOrDefault(d => d.IsDefault) ?? _devices.FirstOrDefault();
        }

        return _devices.FirstOrDefault(d => d.Id == selectedDeviceId)
            ?? _devices.FirstOrDefault(d => d.IsDefault)
            ?? _devices.FirstOrDefault();
    }
}
