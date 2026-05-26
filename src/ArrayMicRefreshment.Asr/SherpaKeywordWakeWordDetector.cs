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

    private long _windowChunks;
    private long _windowDecodePasses;
    private long _windowEmptyKeywordDecodes;
    private long _windowSkippedNotRunning;
    private long _windowSkippedSampleRate;
    private long _windowLoudChunks;
    private float _windowPeakInputRms;
    private float _windowPeakAgcRms;
    private float _windowPeakAgcGain;
    private long _lifetimeDetections;
    private DateTimeOffset _windowStartedUtc = DateTimeOffset.UtcNow;
    private string? _lastKeywordLine;

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
            "Sherpa KWS ready (phrase={Phrase}, sensitivity={Sensitivity}, threshold={Threshold:F3}, score={Score:F1}, keywordsFile={File})",
            _phrase,
            _sensitivity,
            _keywordsThreshold,
            _keywordsScore,
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
            ResetStreamingState(rearm: false);
            _running = true;
            Log.Information(
                "[WAKE-DIAG] KWS started (phrase={Phrase}, running={Running})",
                _phrase,
                _running);
        }
    }

    public void RearmAfterDictation()
    {
        lock (_gate)
        {
            ResetStreamingState(rearm: true);
            _running = true;
            Log.Information(
                "[WAKE-DIAG] KWS rearmed after dictation (phrase={Phrase}, threshold={Threshold:F3}, score={Score:F1})",
                _phrase,
                _keywordsThreshold,
                _keywordsScore);
        }
    }

    private void ResetStreamingState(bool rearm)
    {
        if (_spotter is not null)
        {
            _stream?.Dispose();
            _stream = _spotter.CreateStream();
        }

        ResetAgc();
        ResetWindowStats();
        if (rearm)
        {
            _windowStartedUtc = DateTimeOffset.UtcNow;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_running)
            {
                Log.Information(
                    "[WAKE-DIAG] KWS stopped (phrase={Phrase}, lifetimeDetections={Detections})",
                    _phrase,
                    _lifetimeDetections);
            }

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
                _keywordsThreshold = 0.05f;
                _keywordsScore = 1.8f;
                _targetRms = 0.18f;
                _maxGain = 120f;
                _attack = 0.96f;
                _release = 0.03f;
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
                _windowSkippedNotRunning++;
                return;
            }

            if (sampleRate != 16000)
            {
                _windowSkippedSampleRate++;
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

            var inputRms = ComputeRms(pcm16Mono);
            TrackInputWindow(inputRms);

            for (var i = 0; i < pcm16Mono.Length; i++)
            {
                floats[i] = pcm16Mono[i] / 32768f;
            }

            ApplyStreamingAgc(floats.AsSpan(0, pcm16Mono.Length));
            var agcRms = ComputeRmsFloat(floats.AsSpan(0, pcm16Mono.Length));
            _windowPeakAgcRms = Math.Max(_windowPeakAgcRms, agcRms);
            _windowPeakAgcGain = Math.Max(_windowPeakAgcGain, _agcGain);
            _windowChunks++;

            _stream.AcceptWaveform(sampleRate, floats);

            while (_spotter.IsReady(_stream))
            {
                _windowDecodePasses++;
                _spotter.Decode(_stream);
                var result = _spotter.GetResult(_stream);
                if (string.IsNullOrEmpty(result.Keyword))
                {
                    _windowEmptyKeywordDecodes++;
                    continue;
                }

                _lifetimeDetections++;
                Log.Information(
                    "[WAKE-DIAG] Sherpa KWS hit keyword={Keyword} (phrase={Phrase}, inputRms={InputRms:F4}, agcRms={AgcRms:F4}, agcGain={Gain:F1}x, decodes={Decodes})",
                    result.Keyword,
                    _phrase,
                    inputRms,
                    agcRms,
                    _agcGain,
                    _windowDecodePasses);
                Log.Information("Sherpa KWS detected keyword: {Keyword}", result.Keyword);
                _spotter.Reset(_stream);
                WakeWordDetected?.Invoke(
                    this,
                    new WakeWordDetectedEventArgs(_phrase, DateTimeOffset.UtcNow));
                break;
            }
        }
    }

    public void FlushPeriodicDiagnostics(WakeWordListenStats listen)
    {
        lock (_gate)
        {
            var analysis = WakeWordDiagnosticAnalyzer.AnalyzeListenPath(listen, DetectorId, _running);
            Log.Information(
                "[WAKE-DIAG] window={WindowSec:F0}s phrase={Phrase} sensitivity={Sensitivity} " +
                "listen={Listening} dictation={Dictation} device={Device}@{Rate}Hz " +
                "captureBytes={CapBytes} chunksFed={ChunksFed} skippedListen={SkipListen} skippedDictation={SkipDictation} " +
                "peakRms={PeakRms:F4} kwsChunks={KwsChunks} kwsDecodes={Decodes} emptyDecodes={EmptyDecodes} " +
                "skippedNotRunning={SkipRun} skippedRate={SkipRate} loudChunks={Loud} " +
                "peakAgcRms={AgcRms:F4} peakAgcGain={AgcGain:F1}x lifetimeHits={Hits} " +
                "kwsThreshold={Th:F3} kwsScore={Score:F1} keywordLine={KeywordLine}",
                (DateTimeOffset.UtcNow - _windowStartedUtc).TotalSeconds,
                _phrase,
                listen.Sensitivity,
                listen.Listening,
                listen.DictationActive,
                listen.DeviceDisplayName ?? "?",
                listen.DeviceSampleRate,
                listen.CaptureBytes,
                listen.ChunksFed,
                listen.ChunksSkippedNotListening,
                listen.ChunksSkippedDictation,
                listen.CapturePeakRms,
                _windowChunks,
                _windowDecodePasses,
                _windowEmptyKeywordDecodes,
                _windowSkippedNotRunning,
                _windowSkippedSampleRate,
                _windowLoudChunks,
                _windowPeakAgcRms,
                _windowPeakAgcGain,
                _lifetimeDetections,
                _keywordsThreshold,
                _keywordsScore,
                _lastKeywordLine ?? "?");
            Log.Information("[WAKE-DIAG] analysis: {Analysis}", analysis);
            ResetWindowStats();
        }
    }

    private void TrackInputWindow(float inputRms)
    {
        _windowPeakInputRms = Math.Max(_windowPeakInputRms, inputRms);
        if (inputRms >= 0.012f)
        {
            _windowLoudChunks++;
        }
    }

    private void ResetWindowStats()
    {
        _windowChunks = 0;
        _windowDecodePasses = 0;
        _windowEmptyKeywordDecodes = 0;
        _windowSkippedNotRunning = 0;
        _windowSkippedSampleRate = 0;
        _windowLoudChunks = 0;
        _windowPeakInputRms = 0;
        _windowPeakAgcRms = 0;
        _windowPeakAgcGain = 0;
        _windowStartedUtc = DateTimeOffset.UtcNow;
    }

    private static float ComputeRms(ReadOnlySpan<short> samples)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        double sum = 0;
        foreach (var s in samples)
        {
            var n = s / 32768.0;
            sum += n * n;
        }

        return (float)Math.Sqrt(sum / samples.Length);
    }

    private static float ComputeRmsFloat(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        double sum = 0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        return (float)Math.Sqrt(sum / samples.Length);
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

        if (!WakeWordKeywordFile.TryWrite(_paths, _keywordsFilePath, _phrase, _keywordsScore, _keywordsThreshold))
        {
            throw new InvalidOperationException(
                $"Failed to encode wake phrase '{_phrase}' for Sherpa KWS.");
        }

        _lastKeywordLine = TryReadFirstKeywordLine(_keywordsFilePath);

        var config = BuildConfig(_paths, _keywordsFilePath, _keywordsThreshold, _keywordsScore);
        _spotter = new KeywordSpotter(config);
        _stream = _spotter.CreateStream();
        ResetAgc();
        Log.Information(
            "[WAKE-DIAG] KWS spotter rebuilt (phrase={Phrase}, threshold={Threshold:F3}, score={Score:F1}, keywordLine={KeywordLine}, encoder={Encoder}, decoder={Decoder})",
            _phrase,
            _keywordsThreshold,
            _keywordsScore,
            _lastKeywordLine ?? "?",
            _paths.EncoderPath,
            _paths.DecoderPath);
    }

    private static string? TryReadFirstKeywordLine(string keywordsFilePath)
    {
        try
        {
            if (!File.Exists(keywordsFilePath))
            {
                return null;
            }

            return File.ReadLines(keywordsFilePath).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        }
        catch
        {
            return null;
        }
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
