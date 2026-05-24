using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Integration.Tests.Support;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Integration.Tests;

[Collection("STA")]
public sealed class EndToEndPipelineTests
{
    [Fact]
    public async Task PttRelease_to_ClipboardSink_with_fake_backend()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await StaTestRunner.RunAsync(async () =>
        {
            await using var server = new FakeOpenAiServer(
                """{"intent":"write_code","confidence":0.9}""",
                "refined output");
            server.Start();

            var settings = new AppSettings
            {
                MasterEnabled = true,
                PromptRefineEnabled = true,
                PasteToCaretEnabled = false,
                ForcedIntent = PromptIntent.Auto,
                SkillsDirectory = SkillsPathResolver.Resolve("skills"),
            };

            var (pipeline, sink, _) = PipelineTestHost.Build(settings, server);
            string? emittedText = null;
            sink.Emitted += (text, _) => emittedText = text;

            await pipeline.ProcessUtteranceAsync(PipelineTestHost.CreateUtterance(), CancellationToken.None);

            Assert.Equal("refined output", emittedText);
            Assert.Equal("refined output", ClipboardAssertions.GetClipboardText());
        });
    }
}

[CollectionDefinition("STA", DisableParallelization = true)]
public sealed class StaCollection
{
}
