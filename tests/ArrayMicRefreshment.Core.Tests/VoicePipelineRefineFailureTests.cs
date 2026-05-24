using System.Net;
using System.Text;
using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Output;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.Core.Tests;

public class VoicePipelineRefineFailureTests
{
    [Fact]
    public async Task OnRefineFailure_UseRawTranscript_returns_raw_when_api_fails()
    {
        var sink = new RecordingSink();
        var settings = new AppSettings
        {
            MasterEnabled = true,
            PromptRefineEnabled = true,
            OnRefineFailure = OnRefineFailure.UseRawTranscript,
            ApiBaseUrl = "https://api.example.com/v1",
        };

        var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve("skills"));
        var handler = new FailingHandler();
        var router = new OpenAiCompatibleIntentRouter(settings, catalog, handler);
        var refiner = new OpenAiCompatiblePromptRefiner(settings, catalog, handler);
        var pipeline = new VoicePipeline(
            settings,
            new StubSpeakerGate { AlwaysPass = true },
            new StubUtteranceAsr(),
            router,
            refiner,
            sink);

        await pipeline.ProcessUtteranceAsync(TestAudioHelper.CreateUtterance(), CancellationToken.None);

        var (text, _) = Assert.Single(sink.Emitted);
        Assert.Contains("ASR stub", text, StringComparison.Ordinal);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
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
