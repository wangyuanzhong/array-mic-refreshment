#if WINDOWS
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ArrayMicRefreshment.Audio;

public sealed class NAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    /// <summary>Include devices Windows still reports (disabled / unplugged), not only Active.</summary>
    private const DeviceState WasapiCaptureStates =
        DeviceState.Active | DeviceState.Unplugged | DeviceState.Disabled;

    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
    {
        var list = new List<AudioDeviceInfo>();
        list.AddRange(EnumerateWasapi());
        list.AddRange(EnumerateMme());
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

        if (selectedDeviceId.StartsWith("wdm:", StringComparison.OrdinalIgnoreCase))
        {
            selectedDeviceId = "mme:" + selectedDeviceId["wdm:".Length..];
        }

        return devices.FirstOrDefault(d => d.Id == selectedDeviceId)
            ?? devices.FirstOrDefault(d => d.IsDefault)
            ?? devices.FirstOrDefault();
    }

    private static IEnumerable<AudioDeviceInfo> EnumerateWasapi()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultIds = CollectDefaultCaptureEndpointIds(enumerator);

        IEnumerable<MMDevice> endpoints;
        try
        {
            endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, WasapiCaptureStates);
        }
        catch
        {
            yield break;
        }

        foreach (var device in endpoints)
        {
            AudioDeviceInfo? info = null;
            try
            {
                if (device.State == DeviceState.NotPresent)
                {
                    continue;
                }

                info = new AudioDeviceInfo
                {
                    Id = $"wasapi:{device.ID}",
                    DisplayName = FormatWasapiDisplayName(device),
                    HostApi = AudioHostApi.Wasapi,
                    IsDefault = defaultIds.Contains(device.ID),
                    DefaultSampleRate = 48000,
                    Channels = 1,
                };
            }
            catch
            {
                // Single bad endpoint must not hide the rest.
            }

            if (info is not null)
            {
                yield return info;
            }
        }
    }

    private static HashSet<string> CollectDefaultCaptureEndpointIds(MMDeviceEnumerator enumerator)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in new[] { Role.Console, Role.Communications, Role.Multimedia })
        {
            try
            {
                ids.Add(enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role).ID);
            }
            catch
            {
                // Role may have no capture default on this machine.
            }
        }

        return ids;
    }

    private static string FormatWasapiDisplayName(MMDevice device)
    {
        var suffix = device.State switch
        {
            DeviceState.Disabled => " (已禁用)",
            DeviceState.Unplugged => " (未插入)",
            _ when device.State != DeviceState.Active => $" ({device.State})",
            _ => string.Empty,
        };

        return $"[WASAPI] {device.FriendlyName}{suffix}";
    }

    private static IEnumerable<AudioDeviceInfo> EnumerateMme()
    {
        int count;
        try
        {
            count = WaveIn.DeviceCount;
        }
        catch
        {
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            AudioDeviceInfo? info = null;
            try
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = string.IsNullOrWhiteSpace(caps.ProductName) ? $"Device {i}" : caps.ProductName.Trim();
                info = new AudioDeviceInfo
                {
                    Id = $"mme:{i}",
                    DisplayName = $"[MME] {name}",
                    HostApi = AudioHostApi.Mme,
                    IsDefault = i == 0,
                    DefaultSampleRate = 44100,
                    Channels = Math.Max(1, caps.Channels),
                };
            }
            catch
            {
                // winmm can throw on individual indices (virtual / driver quirks).
            }

            if (info is not null)
            {
                yield return info;
            }
        }
    }
}
#endif
