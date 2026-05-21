using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Output;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.Core.Tests;

public class VoicePipelineTests
{
    [Fact]
    public async Task MasterDisabled_does_not_emit()
    {
        var sink = new RecordingSink();
        var pipeline = CreatePipeline(new AppSettings { MasterEnabled = false }, sink);

        await pipeline.ProcessUtteranceAsync(CreateUtterance(), CancellationToken.None);

        Assert.Empty(sink.Emitted);
    }

    [Fact]
    public async Task SpeakerRejected_does_not_emit()
    {
        var sink = new RecordingSink();
        var pipeline = new VoicePipeline(
            new AppSettings { MasterEnabled = true },
            new StubSpeakerGate { AlwaysPass = false },
            new StubUtteranceAsr(),
            new StubIntentRouter(),
            new StubPromptRefiner(enabled: false),
            sink);

        await pipeline.ProcessUtteranceAsync(CreateUtterance(), CancellationToken.None);

        Assert.Empty(sink.Emitted);
    }

    [Fact]
    public async Task RefineEnabled_uses_refined_text_not_raw()
    {
        var sink = new RecordingSink();
        var pipeline = CreatePipeline(
            new AppSettings
            {
                MasterEnabled = true,
                PromptRefineEnabled = true,
                ForcedIntent = PromptIntent.CodeEditing,
            },
            sink,
            refinerEnabled: true);

        await pipeline.ProcessUtteranceAsync(CreateUtterance(), CancellationToken.None);

        var (text, _) = Assert.Single(sink.Emitted);
        Assert.StartsWith("[refined:CodeEditing]", text, StringComparison.Ordinal);
        // Production PromptRefiner must not echo raw ASR; stub refiner still embeds raw for now.
    }

    [Fact]
    public async Task RefineDisabled_emits_raw_transcript()
    {
        var sink = new RecordingSink();
        var pipeline = CreatePipeline(
            new AppSettings { MasterEnabled = true, PromptRefineEnabled = false },
            sink);

        await pipeline.ProcessUtteranceAsync(CreateUtterance(), CancellationToken.None);

        var (text, _) = Assert.Single(sink.Emitted);
        Assert.Contains("ASR stub", text, StringComparison.Ordinal);
    }

    private static VoicePipeline CreatePipeline(
        AppSettings settings,
        RecordingSink sink,
        bool refinerEnabled = false) =>
        new(
            settings,
            new StubSpeakerGate { AlwaysPass = true },
            new StubUtteranceAsr(),
            new StubIntentRouter(),
            new StubPromptRefiner(refinerEnabled),
            sink);

    private static AudioUtterance CreateUtterance() => new()
    {
        Pcm16LeMono = new byte[320],
        SampleRate = 16000,
        Duration = TimeSpan.FromMilliseconds(20),
    };

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
