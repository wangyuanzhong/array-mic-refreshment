namespace ArrayMicRefreshment.Audio;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public AudioHostApi HostApi { get; init; }
    public bool IsDefault { get; init; }
    public int DefaultSampleRate { get; init; }
    public int Channels { get; init; }
}
