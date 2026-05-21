using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Output;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.Core.Tests;

public class VoicePipelineModelMissingTests
{
    [Fact]
    public async Task Pipeline_with_stubs_does_not_throw_when_asr_factory_would_fail()
    {
        var sink = new RecordingSink();
        Assert.Throws<ModelNotFoundException>(() =>
            SenseVoiceAsr.CreateFromSettings(new AppSettings { ModelsDirectory = GetEmptyModelsDir() }));

        var pipeline = new VoicePipeline(
            new AppSettings { MasterEnabled = true },
            new StubSpeakerGate { AlwaysPass = true },
            new StubUtteranceAsr(),
            new StubIntentRouter(),
            new StubPromptRefiner(enabled: false),
            sink);

        await pipeline.ProcessUtteranceAsync(
            new AudioUtterance
            {
                Pcm16LeMono = new byte[320],
                SampleRate = 16000,
                Duration = TimeSpan.FromMilliseconds(10),
            },
            CancellationToken.None);

        Assert.Single(sink.Emitted);
    }

    private static string GetEmptyModelsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "amr-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class RecordingSink : ITranscriptSink
    {
        public List<(string Text, bool Paste)> Emitted { get; } = new();

        public Task EmitAsync(string textToClipboard, bool pasteToCaret, CancellationToken cancellationToken)
        {
            Emitted.Add((textToClipboard, pasteToCaret));
            return Task.CompletedTask;
        }
    }
}
