using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Core.Tests;

public class SenseVoiceAsrTests
{
    [Fact]
    public void CreateFromSettings_throws_when_models_directory_empty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "amr-test-models-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var settings = new AppSettings { ModelsDirectory = dir };
            var ex = Assert.Throws<ModelNotFoundException>(() => SenseVoiceAsr.CreateFromSettings(settings));
            Assert.Contains("download-models.ps1", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RecognizeUtteranceAsync_uses_backend_and_strips_tags()
    {
        var backend = new FakeSenseVoiceBackend("<|en|><|NEUTRAL|><|Speech|>hello");
        using var asr = new SenseVoiceAsr(backend);
        var text = await asr.RecognizeUtteranceAsync(
            new AudioUtterance
            {
                Pcm16LeMono = new byte[320],
                SampleRate = 16000,
                Duration = TimeSpan.FromMilliseconds(10),
            },
            CancellationToken.None);

        Assert.Equal("hello", text);
    }

    private sealed class FakeSenseVoiceBackend : IOfflineSenseVoiceBackend
    {
        private readonly string _text;

        public FakeSenseVoiceBackend(string text) => _text = text;

        public string Decode(ReadOnlyMemory<float> samples, int sampleRate) => _text;

        public void Dispose()
        {
        }
    }
}
