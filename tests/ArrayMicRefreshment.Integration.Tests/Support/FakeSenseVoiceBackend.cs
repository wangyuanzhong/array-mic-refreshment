using ArrayMicRefreshment.Asr;

namespace ArrayMicRefreshment.Integration.Tests.Support;

internal sealed class FakeSenseVoiceBackend : IOfflineSenseVoiceBackend
{
    public string RawText { get; init; } = "你好<|HAPPY|>世界";

    public string Decode(ReadOnlyMemory<float> samples, int sampleRate) => RawText;

    public void Dispose()
    {
    }
}
