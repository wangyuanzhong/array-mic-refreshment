namespace ArrayMicRefreshment.Asr;

public interface IOfflineAsrBackend : IDisposable
{
    string Decode(ReadOnlyMemory<float> samples, int sampleRate);

    void Warmup() => Decode(new float[AsrInferenceTuning.WarmupSampleCount], 16_000);
}

/// <summary>Backward-compatible alias.</summary>
public interface IOfflineSenseVoiceBackend : IOfflineAsrBackend;
