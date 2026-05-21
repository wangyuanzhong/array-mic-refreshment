using SherpaOnnx;

namespace ArrayMicRefreshment.Speaker;

public sealed class SherpaSpeakerEmbeddingBackend : ISpeakerEmbeddingBackend
{
    private readonly SpeakerEmbeddingExtractor _extractor;

    public SherpaSpeakerEmbeddingBackend(SpeakerModelPaths paths, int numThreads = 2)
    {
        var config = new SpeakerEmbeddingExtractorConfig
        {
            Model = paths.ModelPath,
            NumThreads = numThreads,
            Debug = 0,
            Provider = "cpu",
        };
        _extractor = new SpeakerEmbeddingExtractor(config);
    }

    public int Dim => _extractor.Dim;

    public float[] ComputeEmbedding(ReadOnlyMemory<float> samples, int sampleRate)
    {
        using var stream = _extractor.CreateStream();
        stream.AcceptWaveform(sampleRate, samples.ToArray());
        stream.InputFinished();

        while (!_extractor.IsReady(stream))
        {
            // Sherpa marks readiness after InputFinished for offline-length utterances.
        }

        return _extractor.Compute(stream);
    }

    public void Dispose() => _extractor.Dispose();
}
