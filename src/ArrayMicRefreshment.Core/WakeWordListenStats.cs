namespace ArrayMicRefreshment.Core;

/// <summary>Capture-path snapshot for periodic wake-word diagnostic logging.</summary>
public sealed class WakeWordListenStats
{
    public bool Listening { get; init; }
    public bool DictationActive { get; init; }
    public string? DeviceDisplayName { get; init; }
    public int DeviceSampleRate { get; init; }
    public int DeviceChannels { get; init; }
    public long CaptureBytes { get; init; }
    public long ChunksFed { get; init; }
    public long ChunksSkippedNotListening { get; init; }
    public long ChunksSkippedDictation { get; init; }
    public double CapturePeakRms { get; init; }
    public TimeSpan ListenDuration { get; init; }
    public string WakePhrase { get; init; } = string.Empty;
    public WakeWordSensitivity Sensitivity { get; init; }
}
