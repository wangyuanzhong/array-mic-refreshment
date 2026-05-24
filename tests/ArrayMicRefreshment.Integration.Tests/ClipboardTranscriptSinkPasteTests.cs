using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Integration.Tests.Support;
using ArrayMicRefreshment.Output;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Integration.Tests;

[Collection("STA")]
public sealed class ClipboardTranscriptSinkPasteTests
{
    [Fact]
    public async Task Emit_with_pasteToCaret_sets_clipboard_and_attempts_paste()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await StaTestRunner.RunAsync(async () =>
        {
            var settings = new AppSettings
            {
                MasterEnabled = true,
                PromptRefineEnabled = false,
                PasteToCaretEnabled = true,
                SkillsDirectory = SkillsPathResolver.Resolve("skills"),
            };

            var sink = new ClipboardTranscriptSink();
            bool emitted = false;
            sink.Emitted += (_, _) => emitted = true;

            var text = "test paste " + Guid.NewGuid();
            await sink.EmitAsync(text, pasteToCaret: true, CancellationToken.None);

            await Task.Delay(600);

            Assert.True(emitted, "Emitted event should have fired");
            var clipboardText = ClipboardAssertions.GetClipboardText();
            Assert.Equal(text, clipboardText);
        });
    }

    [Fact]
    public async Task Pipeline_end_to_end_with_paste_enabled()
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
                PasteToCaretEnabled = true,
                ForcedIntent = PromptIntent.Auto,
                SkillsDirectory = SkillsPathResolver.Resolve("skills"),
            };

            var (pipeline, sink, _) = PipelineTestHost.Build(settings, server);
            string? emittedText = null;
            bool pasteFlag = false;
            sink.Emitted += (text, paste) =>
            {
                emittedText = text;
                pasteFlag = paste;
            };

            await pipeline.ProcessUtteranceAsync(PipelineTestHost.CreateUtterance(), CancellationToken.None);

            await Task.Delay(600);

            Assert.Equal("refined output", emittedText);
            Assert.True(pasteFlag, "Paste flag should be true when PasteToCaretEnabled is true");
            Assert.Equal("refined output", ClipboardAssertions.GetClipboardText());
        });
    }

    [Fact]
    public async Task PasteToCaret_false_only_sets_clipboard_without_paste_attempt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await StaTestRunner.RunAsync(async () =>
        {
            var sink = new ClipboardTranscriptSink();
            bool emitted = false;
            bool pasteFlag = false;
            sink.Emitted += (_, paste) =>
            {
                emitted = true;
                pasteFlag = paste;
            };

            var text = "no paste " + Guid.NewGuid();
            await sink.EmitAsync(text, pasteToCaret: false, CancellationToken.None);

            await Task.Delay(200);

            Assert.True(emitted, "Emitted event should have fired");
            Assert.False(pasteFlag, "Paste flag should be false");
            Assert.Equal(text, ClipboardAssertions.GetClipboardText());
        });
    }

    [Fact]
    public async Task SetPasteTarget_and_Emit_with_valid_window()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await StaTestRunner.RunAsync(async () =>
        {
            using var form = new System.Windows.Forms.Form();
            var textBox = new System.Windows.Forms.TextBox
            {
                Multiline = true,
                Width = 400,
                Height = 200,
            };
            form.Controls.Add(textBox);
            form.Show();
            form.Activate();
            await Task.Delay(100);

            var sink = new ClipboardTranscriptSink();
            sink.SetPasteTarget(form.Handle, textBox.Handle);

            var text = "pasted text " + Guid.NewGuid();
            System.Windows.Forms.Clipboard.SetText(text);
            await sink.EmitAsync(text, pasteToCaret: true, CancellationToken.None);

            await Task.Delay(800);

            var clipboardText = ClipboardAssertions.GetClipboardText();
            Assert.Equal(text, clipboardText);

            form.Close();
        });
    }
}
