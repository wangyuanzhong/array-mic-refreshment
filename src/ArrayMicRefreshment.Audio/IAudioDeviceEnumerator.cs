namespace ArrayMicRefreshment.Audio;

public interface IAudioDeviceEnumerator
{
    IReadOnlyList<AudioDeviceInfo> ListCaptureDevices();

    AudioDeviceInfo? ResolveDevice(string? selectedDeviceId);
}
