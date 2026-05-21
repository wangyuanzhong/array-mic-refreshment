namespace ArrayMicRefreshment.Speaker;

public interface ISpeakerEmbeddingBackend : IDisposable
{
    int Dim { get; }

    float[] ComputeEmbedding(ReadOnlyMemory<float> samples, int sampleRate);
}
