using ArrayMicRefreshment.Core;
using Serilog;
using SherpaOnnx;

namespace ArrayMicRefreshment.Asr;

/// <summary>
/// Sherpa-ONNX streaming keyword spotter for Chinese wake phrases.
/// Requires <see cref="WakeWordModelPaths"/> under the configured models directory.
/// </summary>
public sealed class SherpaKeywordWakeWordDetector : IWakeWordDetector
{
    private readonly WakeWordModelPaths _paths;
    private readonly string _keywordsFilePath;
    private readonly object _gate = new();
    private KeywordSpotter? _spotter;
    private OnlineStream? _stream;
    private string _phrase;
    private float[]? _floatScratch;
    private bool _running;
    private bool _disposed;
    private float _agcGain = 1f;
    private float _noiseFloorEstimate = 0.001f;
    private WakeWordSensitivity _sensitivity = WakeWordSensitivity.High;
    private float _keywordsThreshold = 0.10f;
    private float _keywordsScore = 2.5f;
    private float _targetRms = 0.12f;
    private float _maxGain = 60f;
    private float _attack = 0.78f;
    private float _release = 0.08f;

    private SherpaKeywordWakeWordDetector(
        WakeWordModelPaths paths,
        string phrase,
        string keywordsFilePath,
        WakeWordSensitivity sensitivity)
    {
        _paths = paths;
        _phrase = string.IsNullOrWhiteSpace(phrase) ? "小助手" : phrase.Trim();
        _keywordsFilePath = keywordsFilePath;
        ApplySensitivityProfile(sensitivity);
        RebuildSpotter();
        Log.Information(
            "Sherpa KWS ready (phrase={Phrase}, keywordsFile={File})",
            _phrase,
            _keywordsFilePath);
    }

    public string DetectorId => "sherpa-kws";

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    public static bool TryCreate(
        string modelsDirectory,
        string phrase,
        out SherpaKeywordWakeWordDetector? detector,
        WakeWordSensitivity sensitivity = WakeWordSensitivity.High)
    {
        detector = null;
        if (!WakeWordModelPaths.TryResolve(modelsDirectory, out var paths))
        {
            return false;
        }

        var keywordsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArrayMicRefreshment");
        var keywordsFile = Path.Combine(keywordsDir, "wake-keywords.txt");

        try
        {
            detector = new SherpaKeywordWakeWordDetector(paths, phrase, keywordsFile, sensitivity);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create Sherpa keyword spotter");
            detector?.Dispose();
            detector = null;
            return false;
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_spotter is not null && _stream is not null)
            {
                _spotter.Reset(_stream);
            }

            ResetAgc();
            _running = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _running = false;
        }
    }

    public void ApplyPhrase(string phrase)
    {
        lock (_gate)
        {
            var normalized = string.IsNullOrWhiteSpace(phrase) ? "小助手" : phrase.Trim();
            if (string.Equals(_phrase, normalized, StringComparison.Ordinal))
            {
                return;
            }

            var wasRunning = _running;
            if (wasRunning)
            {
                Stop();
            }

            _phrase = normalized;
            RebuildSpotter();
            if (wasRunning)
            {
                Start();
            }
        }
    }

    public void ApplyWakeSensitivity(WakeWordSensitivity sensitivity)
    {
        lock (_gate)
        {
            if (_sensitivity == sensitivity && _spotter is not null)
            {
                return;
            }

            ApplySensitivityProfile(sensitivity);
            ResetAgc();

            if (_spotter is null)
            {
                return;
            }

            var wasRunning = _running;
            if (wasRunning)
            {
                Stop();
            }

            RebuildSpotter();
            if (wasRunning)
            {
                Start();
            }

            Log.Information(
                "Sherpa KWS sensitivity={Sensitivity} (threshold={Threshold:F2}, targetRms={Target:F2}, maxGain={MaxGain:F0}x)",
                sensitivity,
                _keywordsThreshold,
                _targetRms,
                _maxGain);
        }
    }

    private void ApplySensitivityProfile(WakeWordSensitivity sensitivity)
    {
        _sensitivity = sensitivity;
        switch (sensitivity)
        {
            case WakeWordSensitivity.Standard:
                _keywordsThreshold = 0.12f;
                _keywordsScore = 2.5f;
                _targetRms = 0.10f;
                _maxGain = 40f;
                _attack = 0.72f;
                _release = 0.10f;
                break;
            case WakeWordSensitivity.Maximum:
                _keywordsThreshold = 0.06f;
                _keywordsScore = 2.0f;
                _targetRms = 0.16f;
                _maxGain = 100f;
                _attack = 0.92f;
                _release = 0.04f;
                break;
            default:
                _keywordsThreshold = 0.10f;
                _keywordsScore = 2.5f;
                _targetRms = 0.12f;
                _maxGain = 60f;
                _attack = 0.78f;
                _release = 0.08f;
                break;
        }
    }

    public void ProcessAudio(ReadOnlySpan<short> pcm16Mono, int sampleRate)
    {
        if (_disposed || pcm16Mono.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (!_running || _spotter is null || _stream is null)
            {
                return;
            }

            if (sampleRate != 16000)
            {
                return;
            }

            float[] floats;
            if (_floatScratch is null || _floatScratch.Length != pcm16Mono.Length)
            {
                floats = new float[pcm16Mono.Length];
                _floatScratch = floats;
            }
            else
            {
                floats = _floatScratch;
            }

            for (var i = 0; i < pcm16Mono.Length; i++)
            {
                floats[i] = pcm16Mono[i] / 32768f;
            }

            ApplyStreamingAgc(floats.AsSpan(0, pcm16Mono.Length));

            _stream.AcceptWaveform(sampleRate, floats);

            while (_spotter.IsReady(_stream))
            {
                _spotter.Decode(_stream);
                var result = _spotter.GetResult(_stream);
                if (string.IsNullOrEmpty(result.Keyword))
                {
                    continue;
                }

                Log.Information("Sherpa KWS detected keyword: {Keyword}", result.Keyword);
                _spotter.Reset(_stream);
                WakeWordDetected?.Invoke(
                    this,
                    new WakeWordDetectedEventArgs(_phrase, DateTimeOffset.UtcNow));
                break;
            }
        }
    }

    private void RebuildSpotter()
    {
        _stream?.Dispose();
        _stream = null;
        _spotter?.Dispose();
        _spotter = null;

        if (_disposed)
        {
            return;
        }

        if (!WakeWordKeywordFile.TryWrite(_paths, _keywordsFilePath, _phrase))
        {
            throw new InvalidOperationException(
                $"Failed to encode wake phrase '{_phrase}' for Sherpa KWS.");
        }

        var config = BuildConfig(_paths, _keywordsFilePath, _keywordsThreshold, _keywordsScore);
        _spotter = new KeywordSpotter(config);
        _stream = _spotter.CreateStream();
        ResetAgc();
    }

    private static KeywordSpotterConfig BuildConfig(
        WakeWordModelPaths paths,
        string keywordsFile,
        float keywordsThreshold,
        float keywordsScore)
    {
        var config = new KeywordSpotterConfig
        {
            FeatConfig = new FeatureConfig
            {
                SampleRate = 16000,
                FeatureDim = 80,
            },
            KeywordsFile = keywordsFile,
            KeywordsScore = keywordsScore,
            KeywordsThreshold = keywordsThreshold,
        };

        config.ModelConfig.Tokens = paths.TokensPath;
        config.ModelConfig.Transducer.Encoder = paths.EncoderPath;
        config.ModelConfig.Transducer.Decoder = paths.DecoderPath;
        config.ModelConfig.Transducer.Joiner = paths.JoinerPath;
        config.ModelConfig.NumThreads = 2;
        config.ModelConfig.Debug = 0;
        config.ModelConfig.Provider = "cpu";
        return config;
    }

    private void ResetAgc()
    {
        _agcGain = 1f;
        _noiseFloorEstimate = 0.001f;
    }

    /// <summary>
    /// Fast-attack streaming AGC for KWS only — smooth gain, soft limiter, does not touch dictation PCM.
    /// </summary>
    private void ApplyStreamingAgc(Span<float> samples)
    {
        if (samples.Length == 0)
        {
            return;
        }

        const float minGain = 0.30f;
        var targetRms = _targetRms;
        var maxGain = _maxGain;
        var attack = _attack;
        var release = _release;

        double sum = 0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        var rms = (float)Math.Sqrt(sum / samples.Length);
        if (rms < _noiseFloorEstimate * 2.5f)
        {
            _noiseFloorEstimate = (_noiseFloorEstimate * 0.96f) + (rms * 0.04f);
        }

        var level = Math.Max(Math.Max(rms, _noiseFloorEstimate), 1e-6f);
        var desired = Math.Clamp(targetRms / level, minGain, maxGain);
        var smooth = desired > _agcGain ? attack : release;
        _agcGain += (desired - _agcGain) * smooth;

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = SoftClip(samples[i] * _agcGain);
        }
    }

    private static float SoftClip(float sample)
    {
        const float knee = 0.92f;
        if (sample > knee)
        {
            return knee + ((sample - knee) / (1f + (sample - knee) * 4f));
        }

        if (sample < -knee)
        {
            return -knee + ((sample + knee) / (1f - (sample + knee) * 4f));
        }

        return sample;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            _running = false;
            _stream?.Dispose();
            _stream = null;
            _spotter?.Dispose();
            _spotter = null;
        }
    }
}
