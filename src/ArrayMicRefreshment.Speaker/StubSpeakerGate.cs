using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

/// <summary>Phase 2: Sherpa speaker embedding verify.</summary>
public sealed class StubSpeakerGate : ISpeakerGate
{
    public bool AlwaysPass { get; set; } = true;

    public Task<SpeakerVerificationResult> VerifyCurrentUserAsync(
        AudioUtterance utterance,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SpeakerVerificationResult(AlwaysPass, Score: 1f, VerificationSkipped: false));
}
