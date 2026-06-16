using Serilog;
using SherpaOnnx;

namespace ArrayMicRefreshment.Asr;

public sealed class SherpaQwen3AsrBackend : IOfflineAsrBackend
{
    private readonly OfflineRecognizer _recognizer;
    private readonly int _numThreads;

    public SherpaQwen3AsrBackend(AsrModelPaths paths, int? numThreads = null)
    {
        if (paths.Qwen3 is null)
        {
            throw new ArgumentException("Qwen3 ASR model files are required.", nameof(paths));
        }

        _numThreads = numThreads ?? AsrInferenceTuning.ResolveNumThreads();
        var qwen = paths.Qwen3;

        var config = new OfflineRecognizerConfig
        {
            FeatConfig = new FeatureConfig
            {
                SampleRate = 16_000,
                FeatureDim = 80,
            },
            ModelConfig = new OfflineModelConfig
            {
                Tokens = string.Empty,
                Qwen3Asr = new OfflineQwen3AsrModelConfig
                {
                    ConvFrontend = qwen.ConvFrontendPath,
                    Encoder = qwen.EncoderPath,
                    Decoder = qwen.DecoderPath,
                    Tokenizer = qwen.TokenizerDir,
                    MaxNewTokens = AsrInferenceTuning.QwenMaxNewTokens,
                    MaxTotalLen = AsrInferenceTuning.QwenMaxTotalLen,
                    Temperature = 1e-6F,
                    TopP = 0.8F,
                    Seed = 42,
                    Hotwords = string.Empty,
                },
                NumThreads = _numThreads,
                Debug = 0,
                Provider = "cpu",
            },
            DecodingMethod = "greedy_search",
        };

        _recognizer = new OfflineRecognizer(config);
        Log.Information(
            "Qwen3-ASR backend ready (threads={Threads}, maxNewTokens={MaxNewTokens}, encoder={Encoder})",
            _numThreads,
            AsrInferenceTuning.QwenMaxNewTokens,
            qwen.EncoderPath);
    }

    public string Decode(ReadOnlyMemory<float> samples, int sampleRate)
    {
        using var stream = _recognizer.CreateStream();
        stream.AcceptWaveform(sampleRate, ToArray(samples));
        _recognizer.Decode(stream);
        return stream.Result.Text ?? string.Empty;
    }

    public void Warmup() => Decode(new float[AsrInferenceTuning.WarmupSampleCount], 16_000);

    public void Dispose() => _recognizer.Dispose();

    private static float[] ToArray(ReadOnlyMemory<float> samples) =>
        samples.IsEmpty ? Array.Empty<float>() : samples.ToArray();
}
