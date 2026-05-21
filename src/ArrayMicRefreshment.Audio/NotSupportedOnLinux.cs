namespace ArrayMicRefreshment.Audio;

/// <summary>Linux/CI placeholder when NAudio is unavailable.</summary>
public sealed class NotSupportedAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices() => Array.Empty<AudioDeviceInfo>();

    public AudioDeviceInfo? ResolveDevice(string? selectedDeviceId) => null;
}

public sealed class NotSupportedCaptureStreamFactory : IAudioCaptureStreamFactory
{
    public IAudioCaptureStream Open(AudioDeviceInfo device) =>
        throw new PlatformNotSupportedException("Audio capture requires Windows (net8.0-windows).");
}
