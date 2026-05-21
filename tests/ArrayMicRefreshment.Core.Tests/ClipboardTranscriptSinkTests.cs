using ArrayMicRefreshment.Output;

namespace ArrayMicRefreshment.Core.Tests;

public class ClipboardTranscriptSinkTests
{
    [Fact]
    public async Task Emit_on_net8_0_fallback_does_not_throw()
    {
        var sink = new ClipboardTranscriptSink();
        var ex = await Record.ExceptionAsync(() =>
            sink.EmitAsync("hello", pasteToCaret: true, CancellationToken.None));
        Assert.Null(ex);
    }
}
