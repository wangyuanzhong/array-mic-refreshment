#if NET8_0_WINDOWS
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ArrayMicRefreshment.Audio;

public sealed class NAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
    {
        var list = new List<AudioDeviceInfo>();
        list.AddRange(EnumerateWasapi());
        list.AddRange(EnumerateMme());
        list.AddRange(EnumerateWdm());
        return list;
    }

    public AudioDeviceInfo? ResolveDevice(string? selectedDeviceId)
    {
        var devices = ListCaptureDevices();
        if (string.IsNullOrEmpty(selectedDeviceId))
        {
            return devices.FirstOrDefault(d => d.IsDefault)
                ?? devices.FirstOrDefault();
        }

        return devices.FirstOrDefault(d => d.Id == selectedDeviceId)
            ?? devices.FirstOrDefault(d => d.IsDefault)
            ?? devices.FirstOrDefault();
    }

    private static IEnumerable<AudioDeviceInfo> EnumerateWasapi()
    {
        using var enumerator = new MMDeviceEnumerator();
        MMDevice? defaultDevice = null;
        try
        {
            defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }
        catch
        {
            // ignored
        }

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            yield return new AudioDeviceInfo
            {
                Id = $"wasapi:{device.ID}",
                DisplayName = $"[WASAPI] {device.FriendlyName}",
                HostApi = AudioHostApi.Wasapi,
                IsDefault = defaultDevice is not null && device.ID == defaultDevice.ID,
                DefaultSampleRate = 48000,
                Channels = 1,
            };
        }
    }

    private static IEnumerable<AudioDeviceInfo> EnumerateMme()
    {
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            yield return new AudioDeviceInfo
            {
                Id = $"mme:{i}",
                DisplayName = $"[MME] {caps.ProductName}",
                HostApi = AudioHostApi.Mme,
                IsDefault = i == 0,
                DefaultSampleRate = 44100,
                Channels = Math.Max(1, caps.Channels),
            };
        }
    }

    private static IEnumerable<AudioDeviceInfo> EnumerateWdm()
    {
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            yield return new AudioDeviceInfo
            {
                Id = $"wdm:{i}",
                DisplayName = $"[WDM] {caps.ProductName}",
                HostApi = AudioHostApi.WdmDirectSound,
                IsDefault = false,
                DefaultSampleRate = 44100,
                Channels = Math.Max(1, caps.Channels),
            };
        }
    }
}
#endif
