using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Output;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.Integration.Tests.Support;

internal static class PipelineTestHost
{
    public static string StripAsrTags(string raw) => SenseVoiceTextExtractor.ExtractPlainText(raw);

    public static (VoicePipeline Pipeline, ClipboardTranscriptSink Sink, string StrippedRaw) Build(
        AppSettings settings,
        FakeOpenAiServer server,
        string asrRaw = "你好<|HAPPY|>世界")
    {
        var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(settings.SkillsDirectory));
        settings.ApiBaseUrl = server.ApiBaseUrl;
        var router = new OpenAiCompatibleIntentRouter(settings, catalog);
        var refiner = new OpenAiCompatiblePromptRefiner(settings, catalog);
        var asr = new SenseVoiceAsr(new FakeSenseVoiceBackend { RawText = asrRaw });
        var sink = new ClipboardTranscriptSink();
        var pipeline = new VoicePipeline(
            settings,
            new StubSpeakerGate { AlwaysPass = true },
            asr,
            router,
            refiner,
            sink);
        return (pipeline, sink, StripAsrTags(asrRaw));
    }

    public static AudioUtterance CreateUtterance() => new()
    {
        Pcm16LeMono = new byte[3200],
        SampleRate = 16000,
        Duration = TimeSpan.FromMilliseconds(200),
    };
}
