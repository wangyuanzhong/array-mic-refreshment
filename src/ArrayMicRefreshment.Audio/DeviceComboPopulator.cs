namespace ArrayMicRefreshment.Audio;

/// <summary>Populates device dropdown selection without WinForms dependencies.</summary>
public static class DeviceComboPopulator
{
    public sealed class DeviceListItem
    {
        public DeviceListItem(AudioDeviceInfo device) => Device = device;

        public AudioDeviceInfo Device { get; }

        public string Id => Device.Id;

        public string DisplayName => Device.DisplayName;
    }

    public static IReadOnlyList<DeviceListItem> BuildItems(IAudioDeviceEnumerator enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        return enumerator.ListCaptureDevices()
            .Select(d => new DeviceListItem(d))
            .ToArray();
    }

    public static int ResolveSelectedIndex(IReadOnlyList<DeviceListItem> items, string? selectedDeviceId)
    {
        if (items.Count == 0)
        {
            return -1;
        }

        if (!string.IsNullOrWhiteSpace(selectedDeviceId))
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Id, selectedDeviceId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Device.IsDefault)
            {
                return i;
            }
        }

        return 0;
    }

    public static string? GetSelectedDeviceId(IReadOnlyList<DeviceListItem> items, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= items.Count)
        {
            return null;
        }

        return items[selectedIndex].Id;
    }
}
