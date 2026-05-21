namespace ArrayMicRefreshment.Core;

public sealed class AudioUtterance
{
    public required byte[] Pcm16LeMono { get; init; }
    public int SampleRate { get; init; } = 16000;
    public TimeSpan Duration { get; init; }
}

public interface IPushToTalkSource
{
    event EventHandler? PttPressed;
    event EventHandler? PttReleased;
    string HotkeyDisplay { get; }
}

public interface IUtteranceAsr
{
    Task<string> RecognizeUtteranceAsync(AudioUtterance utterance, CancellationToken cancellationToken);
}

public interface ISpeakerGate
{
    Task<bool> VerifyCurrentUserAsync(AudioUtterance utterance, CancellationToken cancellationToken);
}

public interface IIntentRouter
{
    Task<(PromptIntent Intent, float Confidence)> RouteAsync(string raw, CancellationToken cancellationToken);
}

public interface IPromptRefiner
{
    bool IsEnabled { get; }
    Task<string> RefineAsync(string raw, PromptIntent intent, CancellationToken cancellationToken);
}

public interface ITranscriptSink
{
    Task EmitAsync(string textToClipboard, bool pasteToCaret, CancellationToken cancellationToken);
}

public interface IAudioPreprocessor
{
    string Name { get; }
    void Process(ReadOnlySpan<short> input, Span<short> output);
}

public sealed class PassThroughPreprocessor : IAudioPreprocessor
{
    public string Name => "pass-through";

    public void Process(ReadOnlySpan<short> input, Span<short> output)
    {
        input.CopyTo(output);
    }
}
