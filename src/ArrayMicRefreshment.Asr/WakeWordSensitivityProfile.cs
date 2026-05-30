using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Asr;

/// <summary>KWS score/threshold used when writing Sherpa keyword lines.</summary>
public static class WakeWordSensitivityProfile
{
    public static (float Score, float Threshold) GetEncodingParams(WakeWordSensitivity sensitivity) =>
        sensitivity switch
        {
            WakeWordSensitivity.Standard => (2.5f, 0.12f),
            WakeWordSensitivity.Maximum => (1.8f, 0.05f),
            _ => (2.5f, 0.10f),
        };
}
