using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Audio.Tests;

public class DeviceComboPopulatorTests
{
    [Fact]
    public void BuildItems_returns_all_devices()
    {
        var devices = new[]
        {
            new AudioDeviceInfo { Id = "a", DisplayName = "Mic A", IsDefault = false, DefaultSampleRate = 16000, Channels = 1 },
            new AudioDeviceInfo { Id = "b", DisplayName = "Mic B", IsDefault = true, DefaultSampleRate = 48000, Channels = 1 },
        };
        var enumerator = new FakeAudioDeviceEnumerator(devices);

        var items = DeviceComboPopulator.BuildItems(enumerator);

        Assert.Equal(2, items.Count);
        Assert.Equal("Mic B", items[1].DisplayName);
    }

    [Fact]
    public void ResolveSelectedIndex_matches_saved_id()
    {
        var devices = new[]
        {
            new AudioDeviceInfo { Id = "a", DisplayName = "Mic A", IsDefault = true, DefaultSampleRate = 16000, Channels = 1 },
            new AudioDeviceInfo { Id = "b", DisplayName = "Mic B", IsDefault = false, DefaultSampleRate = 16000, Channels = 1 },
        };
        var items = DeviceComboPopulator.BuildItems(new FakeAudioDeviceEnumerator(devices));

        Assert.Equal(1, DeviceComboPopulator.ResolveSelectedIndex(items, "b"));
    }

    [Fact]
    public void ResolveSelectedIndex_falls_back_to_default_device()
    {
        var devices = new[]
        {
            new AudioDeviceInfo { Id = "a", DisplayName = "Mic A", IsDefault = false, DefaultSampleRate = 16000, Channels = 1 },
            new AudioDeviceInfo { Id = "b", DisplayName = "Mic B", IsDefault = true, DefaultSampleRate = 16000, Channels = 1 },
        };
        var items = DeviceComboPopulator.BuildItems(new FakeAudioDeviceEnumerator(devices));

        Assert.Equal(1, DeviceComboPopulator.ResolveSelectedIndex(items, "missing"));
    }

    [Fact]
    public void ResolveSelectedIndex_returns_first_when_no_default()
    {
        var devices = new[]
        {
            new AudioDeviceInfo { Id = "a", DisplayName = "Mic A", IsDefault = false, DefaultSampleRate = 16000, Channels = 1 },
            new AudioDeviceInfo { Id = "b", DisplayName = "Mic B", IsDefault = false, DefaultSampleRate = 16000, Channels = 1 },
        };
        var items = DeviceComboPopulator.BuildItems(new FakeAudioDeviceEnumerator(devices));

        Assert.Equal(0, DeviceComboPopulator.ResolveSelectedIndex(items, null));
    }

    [Fact]
    public void GetSelectedDeviceId_round_trips()
    {
        var devices = new[]
        {
            new AudioDeviceInfo { Id = "x", DisplayName = "X", IsDefault = true, DefaultSampleRate = 16000, Channels = 1 },
        };
        var items = DeviceComboPopulator.BuildItems(new FakeAudioDeviceEnumerator(devices));

        Assert.Equal("x", DeviceComboPopulator.GetSelectedDeviceId(items, 0));
    }
}
