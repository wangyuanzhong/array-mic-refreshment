using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Integration.Tests.Support;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Integration.Tests;

[Collection("STA")]
public sealed class OnRefineFailureMatrixTests
{
    public static TheoryData<OnRefineFailure, FakeOpenAiFaultMode> FailureMatrix => new()
    {
        { OnRefineFailure.UseRawTranscript, FakeOpenAiFaultMode.Unauthorized },
        { OnRefineFailure.UseRawTranscript, FakeOpenAiFaultMode.ServerError },
        { OnRefineFailure.UseRawTranscript, FakeOpenAiFaultMode.Slow },
        { OnRefineFailure.ShowError, FakeOpenAiFaultMode.Unauthorized },
        { OnRefineFailure.ShowError, FakeOpenAiFaultMode.ServerError },
        { OnRefineFailure.KeepLast, FakeOpenAiFaultMode.Unauthorized },
        { OnRefineFailure.KeepLast, FakeOpenAiFaultMode.ServerError },
    };

    [Theory]
    [MemberData(nameof(FailureMatrix))]
    public async Task Refine_failure_matrix(OnRefineFailure mode, FakeOpenAiFaultMode fault)
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
            server.FaultMode = fault;
            server.Start();

            var settings = new AppSettings
            {
                MasterEnabled = true,
                PromptRefineEnabled = true,
                OnRefineFailure = mode,
                PasteToCaretEnabled = false,
                SkillsDirectory = SkillsPathResolver.Resolve("skills"),
            };

            var (pipeline, sink, strippedRaw) = PipelineTestHost.Build(settings, server);
            var emitted = new List<string>();
            sink.Emitted += (text, _) => emitted.Add(text);

            const string previous = "previous clipboard value";
            ClipboardAssertions.SetClipboardText(previous);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (mode == OnRefineFailure.ShowError)
            {
                await Assert.ThrowsAsync<RefineApiException>(() =>
                    pipeline.ProcessUtteranceAsync(PipelineTestHost.CreateUtterance(), cts.Token)).ConfigureAwait(false);
                Assert.Empty(emitted);
                Assert.Equal(previous, ClipboardAssertions.GetClipboardText());
                return;
            }

            if (mode == OnRefineFailure.KeepLast)
            {
                await Assert.ThrowsAsync<RefineApiException>(() =>
                    pipeline.ProcessUtteranceAsync(PipelineTestHost.CreateUtterance(), cts.Token)).ConfigureAwait(false);
                Assert.Empty(emitted);
                Assert.Equal(previous, ClipboardAssertions.GetClipboardText());
                return;
            }

            await pipeline.ProcessUtteranceAsync(PipelineTestHost.CreateUtterance(), cts.Token).ConfigureAwait(false);
            Assert.Single(emitted);
            Assert.Equal(strippedRaw, emitted[0]);
            Assert.Equal(strippedRaw, ClipboardAssertions.GetClipboardText());
        }).ConfigureAwait(false);
    }
}
