using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Output;

/// <summary>Phase 3: Clipboard + SendInput paste on Windows.</summary>
public sealed class StubTranscriptSink : ITranscriptSink
{
    public event Action<string, bool>? Emitted;

    public Task EmitAsync(string textToClipboard, bool pasteToCaret, CancellationToken cancellationToken)
    {
        Emitted?.Invoke(textToClipboard, pasteToCaret);
        return Task.CompletedTask;
    }
}
