using SherpaOnnx;

namespace ArrayMicRefreshment.Asr;

public sealed class SherpaSenseVoiceBackend : IOfflineSenseVoiceBackend
{
    private readonly OfflineRecognizer _recognizer;

    public SherpaSenseVoiceBackend(AsrModelPaths paths, int? numThreads = null)
    {
        var threads = numThreads ?? AsrInferenceTuning.ResolveNumThreads();
        var config = new OfflineRecognizerConfig
        {
            FeatConfig = new FeatureConfig
            {
                SampleRate = 16000,
                FeatureDim = 80,
            },
            ModelConfig = new OfflineModelConfig
            {
                Tokens = paths.TokensPath,
                SenseVoice = new OfflineSenseVoiceModelConfig
                {
                    Model = paths.ModelPath,
                    UseInverseTextNormalization = 1,
                },
                NumThreads = threads,
                Debug = 0,
                Provider = "cpu",
            },
            DecodingMethod = "greedy_search",
        };

        _recognizer = new OfflineRecognizer(config);
    }

    public string Decode(ReadOnlyMemory<float> samples, int sampleRate)
    {
        using var stream = _recognizer.CreateStream();
        stream.AcceptWaveform(sampleRate, samples.ToArray());
        _recognizer.Decode(stream);
        return stream.Result.Text ?? string.Empty;
    }

    public void Dispose() => _recognizer.Dispose();
}
