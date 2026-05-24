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
    private readonly ManualResetEventSlim _recordingStopped = new(false);
    private IWaveIn? _waveIn;
    private WaveFormat? _waveFormat;
    private bool _started;

    public NAudioCaptureStream(AudioDeviceInfo device)
    {
        _device = device;
        SampleRate = device.DefaultSampleRate > 0 ? device.DefaultSampleRate : 48000;
        Channels = Math.Max(1, device.Channels);
        BitsPerSample = 16;
    }

    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public int BitsPerSample { get; private set; }

    public event EventHandler<ReadOnlyMemory<byte>>? DataAvailable;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _recordingStopped.Reset();
        _waveIn = CreateWaveIn();
        _waveFormat = _waveIn.WaveFormat;
        SampleRate = _waveFormat.SampleRate;
        Channels = _waveFormat.Channels;
        BitsPerSample = 16;

        _waveIn.DataAvailable += OnWaveInDataAvailable;
        _waveIn.RecordingStopped += OnWaveInRecordingStopped;
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
        _recordingStopped.Wait(TimeSpan.FromMilliseconds(800));
        _started = false;
    }

    private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_waveFormat is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var pcm16 = WaveBufferConverter.ToPcm16Le(e.Buffer, e.BytesRecorded, _waveFormat);
        if (pcm16.Length > 0)
        {
            DataAvailable?.Invoke(this, pcm16);
        }
    }

    private void OnWaveInRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _recordingStopped.Set();
    }

    public void Dispose()
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnWaveInDataAvailable;
            _waveIn.RecordingStopped -= OnWaveInRecordingStopped;
        }

        Stop();
        _waveIn?.Dispose();
        _waveIn = null;
        _recordingStopped.Dispose();
    }

    private IWaveIn CreateWaveIn()
    {
        if (_device.HostApi == AudioHostApi.Wasapi && _device.Id.StartsWith("wasapi:", StringComparison.Ordinal))
        {
            var mmId = _device.Id["wasapi:".Length..];
            using var enumerator = new MMDeviceEnumerator();
            MMDevice mm;
            try
            {
                mm = enumerator.GetDevice(mmId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法打开录音设备「{_device.DisplayName}」。请在 Windows 声音设置中启用该设备，或改选其它设备。",
                    ex);
            }

            if (mm.State is DeviceState.Disabled or DeviceState.Unplugged or DeviceState.NotPresent)
            {
                throw new InvalidOperationException(
                    $"录音设备「{_device.DisplayName}」当前不可用（{mm.State}）。请插入/启用后重试。");
            }

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
