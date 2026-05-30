#if WINDOWS
using SherpaOnnx;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Silero VAD via Sherpa-ONNX. When <c>silero_vad.onnx</c> is missing, defers to a light energy tail check.
/// </summary>
public sealed class SileroVoiceActivityDetector : IVoiceActivityDetector, IDisposable
{
    private const int TargetSampleRate = 16000;

    private readonly VoiceActivityDetector? _vad;
    private readonly bool _energyOnly;
    private TimeSpan _requiredSilence = TimeSpan.FromMilliseconds(500);
    private DateTimeOffset? _lastSpeechUtc;
    private bool _hadSpeech;
    private bool _disposed;

    public SileroVoiceActivityDetector(string? modelsDirectory = null, TimeSpan? initialSilence = null)
    {
        if (initialSilence is { } s)
        {
            _requiredSilence = s;
        }

        var root = modelsDirectory ?? "models";
        var candidate = Path.Combine(root, "silero_vad.onnx");
        if (!File.Exists(candidate))
        {
            _energyOnly = true;
            Log.Warning(
                "Silero VAD model not found at {Path}; wake/PTT VAD uses energy fallback (noisy rooms may need the model)",
                candidate);
            return;
        }

        try
        {
            var minSilenceSec = (float)Math.Clamp(_requiredSilence.TotalSeconds, 0.25, 8.0);
            var config = new VadModelConfig
            {
                SileroVad = new SileroVadModelConfig
                {
                    Model = candidate,
                    Threshold = 0.45f,
                    MinSilenceDuration = minSilenceSec,
                    MinSpeechDuration = 0.2f,
                    WindowSize = 512,
                    MaxSpeechDuration = 120f,
                },
                SampleRate = TargetSampleRate,
                NumThreads = 1,
                Provider = "cpu",
                Debug = 0,
            };
            _vad = new VoiceActivityDetector(config, bufferSizeInSeconds: 60f);
            Log.Information(
                "Silero VAD loaded from {Path} (minSilence={MinSilenceMs}ms)",
                candidate,
                (int)(minSilenceSec * 1000));
        }
        catch (Exception ex)
        {
            _energyOnly = true;
            Log.Warning(ex, "Failed to initialize Sherpa Silero VAD; using energy fallback");
        }
    }

    public bool IsAvailable => _vad is not null && !_energyOnly;

    public bool HadSpeechSinceReset => _hadSpeech;

    public DateTimeOffset? LastSpeechActivityUtc => _lastSpeechUtc;

    public void ConfigureSilenceDuration(TimeSpan silence)
    {
        _requiredSilence = silence;
        if (_vad is null || _energyOnly)
        {
            return;
        }

        // Sherpa VAD reads MinSilenceDuration only at construction — recreate on setting change.
        // Callers should invoke this sparingly (e.g. when settings are saved).
    }

    public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate)
    {
        if (_disposed || mono16Samples.Length == 0)
        {
            return false;
        }

        if (_vad is null || _energyOnly)
        {
            return EnergyFallbackEndOfSpeech(mono16Samples);
        }

        if (sampleRate != TargetSampleRate)
        {
            Log.Debug("VAD expected {Expected}Hz, got {Actual}Hz", TargetSampleRate, sampleRate);
        }

        var floats = new float[mono16Samples.Length];
        for (var i = 0; i < mono16Samples.Length; i++)
        {
            floats[i] = mono16Samples[i] / 32768f;
        }

        _vad.AcceptWaveform(floats);

        var speechNow = _vad.IsSpeechDetected();
        while (!_vad.IsEmpty())
        {
            _vad.Pop();
            speechNow = true;
        }

        if (speechNow)
        {
            _hadSpeech = true;
            _lastSpeechUtc = DateTimeOffset.UtcNow;
            return false;
        }

        if (!_hadSpeech || _lastSpeechUtc is null)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _lastSpeechUtc.Value >= _requiredSilence;
    }

    public void Reset()
    {
        _vad?.Reset();
        _lastSpeechUtc = null;
        _hadSpeech = false;
    }

    private bool EnergyFallbackEndOfSpeech(ReadOnlySpan<short> samples)
    {
        if (EnergyTailLooksLikeSilence(samples))
        {
            if (_hadSpeech && _lastSpeechUtc is not null)
            {
                return DateTimeOffset.UtcNow - _lastSpeechUtc.Value >= _requiredSilence;
            }

            return false;
        }

        _hadSpeech = true;
        _lastSpeechUtc = DateTimeOffset.UtcNow;
        return false;
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vad?.Dispose();
    }
}
#endif
