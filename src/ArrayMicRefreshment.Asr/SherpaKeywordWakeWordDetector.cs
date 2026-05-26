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

    private SherpaKeywordWakeWordDetector(
        WakeWordModelPaths paths,
        string phrase,
        string keywordsFilePath)
    {
        _paths = paths;
        _phrase = string.IsNullOrWhiteSpace(phrase) ? "小助手" : phrase.Trim();
        _keywordsFilePath = keywordsFilePath;
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

    public static bool TryCreate(string modelsDirectory, string phrase, out SherpaKeywordWakeWordDetector? detector)
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
            detector = new SherpaKeywordWakeWordDetector(paths, phrase, keywordsFile);
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

        var config = BuildConfig(_paths, _keywordsFilePath);
        _spotter = new KeywordSpotter(config);
        _stream = _spotter.CreateStream();
        ResetAgc();
    }

    private static KeywordSpotterConfig BuildConfig(WakeWordModelPaths paths, string keywordsFile)
    {
        var config = new KeywordSpotterConfig
        {
            FeatConfig = new FeatureConfig
            {
                SampleRate = 16000,
                FeatureDim = 80,
            },
            KeywordsFile = keywordsFile,
            KeywordsScore = 2.5f,
            KeywordsThreshold = 0.15f,
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

        const float targetRms = 0.08f;
        const float maxGain = 20f;
        const float minGain = 0.65f;
        const float attack = 0.62f;
        const float release = 0.14f;

        double sum = 0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        var rms = (float)Math.Sqrt(sum / samples.Length);
        if (rms < _noiseFloorEstimate * 3f)
        {
            _noiseFloorEstimate = (_noiseFloorEstimate * 0.97f) + (rms * 0.03f);
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
