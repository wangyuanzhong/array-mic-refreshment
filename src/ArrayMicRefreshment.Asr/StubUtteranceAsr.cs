using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Asr;

/// <summary>Phase 3: Sherpa-ONNX SenseVoice OfflineRecognizer.</summary>
public sealed class StubUtteranceAsr : IUtteranceAsr
{
    public string ModelId => "stub";

    public Task<string> RecognizeUtteranceAsync(AudioUtterance utterance, CancellationToken cancellationToken) =>
        Task.FromResult("[ASR stub — run download-models.ps1 and integrate SenseVoice in Phase 3]");
}
