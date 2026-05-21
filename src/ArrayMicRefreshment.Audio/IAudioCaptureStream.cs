namespace ArrayMicRefreshment.Audio;

/// <summary>Virtual capture stream for tests and NAudio-backed recording.</summary>
public interface IAudioCaptureStream : IDisposable
{
    int SampleRate { get; }
    int Channels { get; }
    int BitsPerSample { get; }

    event EventHandler<ReadOnlyMemory<byte>>? DataAvailable;

    void Start();

    void Stop();
}

public interface IAudioCaptureStreamFactory
{
    IAudioCaptureStream Open(AudioDeviceInfo device);
}
