namespace ArrayMicRefreshment.Asr;

public interface IOfflineSenseVoiceBackend : IDisposable
{
    string Decode(ReadOnlyMemory<float> samples, int sampleRate);
}
