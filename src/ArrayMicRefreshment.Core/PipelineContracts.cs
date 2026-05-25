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

/// <summary>
/// Produces <see cref="AudioUtterance"/> after wake-word detection and dictation session end.
/// </summary>
public interface IWakeWordCaptureService : IDisposable
{
    event EventHandler<UtteranceCaptureEventArgs>? UtteranceReady;

    event EventHandler<Exception>? CaptureFailed;

    event EventHandler<string>? CaptureEmpty;

    event EventHandler<string>? StatusChanged;

    bool IsListening { get; }

    bool IsDictationActive { get; }

    void StartListening();

    void StopListening();
}

public interface IUtteranceAsr
{
    /// <summary>Identifier of the currently loaded ASR model (for diagnostics).</summary>
    string ModelId { get; }

    Task<string> RecognizeUtteranceAsync(AudioUtterance utterance, CancellationToken cancellationToken);
}

public interface ISpeakerGate
{
    Task<SpeakerVerificationResult> VerifyCurrentUserAsync(
        AudioUtterance utterance,
        CancellationToken cancellationToken);
}

public interface IIntentRouter
{
    Task<(PromptIntent Intent, float Confidence)> RouteAsync(string raw, CancellationToken cancellationToken);
    void ApplySettings(AppSettings settings);
}

public interface IPromptRefiner
{
    bool IsEnabled { get; }
    Task<string> RefineAsync(string raw, PromptIntent intent, CancellationToken cancellationToken);
    void ApplySettings(AppSettings settings);
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
