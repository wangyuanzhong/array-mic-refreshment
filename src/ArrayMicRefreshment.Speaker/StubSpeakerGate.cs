using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

/// <summary>Phase 2: Sherpa speaker embedding verify.</summary>
public sealed class StubSpeakerGate : ISpeakerGate
{
    public bool AlwaysPass { get; set; } = true;

    public Task<bool> VerifyCurrentUserAsync(AudioUtterance utterance, CancellationToken cancellationToken) =>
        Task.FromResult(AlwaysPass);
}
