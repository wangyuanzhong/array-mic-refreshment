namespace ArrayMicRefreshment.Asr;

public interface IOfflineAsrBackend : IDisposable
{
    string Decode(ReadOnlyMemory<float> samples, int sampleRate);
}

/// <summary>Backward-compatible alias.</summary>
public interface IOfflineSenseVoiceBackend : IOfflineAsrBackend;
