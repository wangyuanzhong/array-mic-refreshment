namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Live capture stream + native-format pre-roll transferred from wake-word listen path (Both mode).
/// Avoids stop/reopen mic gap that clips PTT word onsets.
/// </summary>
public sealed class PttCaptureHandoff
{
    public PttCaptureHandoff(
        IAudioCaptureStream stream,
        string deviceId,
        byte[] preRollNativePcm)
    {
        Stream = stream;
        DeviceId = deviceId;
        PreRollNativePcm = preRollNativePcm;
    }

    public IAudioCaptureStream Stream { get; }
    public string DeviceId { get; }
    public byte[] PreRollNativePcm { get; }
}
