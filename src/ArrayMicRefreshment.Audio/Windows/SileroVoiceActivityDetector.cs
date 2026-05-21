#if NET8_0_WINDOWS
namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Silero VAD hook (ONNX). Loads model when present under models/; otherwise no-ops.
/// Phase 1: energy-based fallback for long-hold assist.
/// </summary>
public sealed class SileroVoiceActivityDetector : IVoiceActivityDetector, IDisposable
{
    private readonly string? _modelPath;
    private bool _disposed;

    public SileroVoiceActivityDetector(string? modelsDirectory = null)
    {
        var root = modelsDirectory ?? "models";
        var candidate = Path.Combine(root, "silero_vad.onnx");
        _modelPath = File.Exists(candidate) ? candidate : null;
    }

    public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate)
    {
        if (_disposed || mono16Samples.Length < 512)
        {
            return false;
        }

        if (_modelPath is not null)
        {
            // ONNX inference hook — full Silero integration in a follow-up when model is bundled.
        }

        return EnergyTailLooksLikeSilence(mono16Samples);
    }

    public void Reset()
    {
    }

    private static bool EnergyTailLooksLikeSilence(ReadOnlySpan<short> samples)
    {
        var tailLen = Math.Min(samples.Length, 1600);
        var tail = samples[^tailLen..];
        double sumSq = 0;
        foreach (var s in tail)
        {
            var n = s / 32768.0;
            sumSq += n * n;
        }

        var rms = Math.Sqrt(sumSq / tailLen);
        return rms < 0.008;
    }

    public void Dispose() => _disposed = true;
}
#endif
