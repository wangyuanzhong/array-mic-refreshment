using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

/// <summary>Used when the speaker ONNX model is missing; verification is skipped.</summary>
public sealed class UnavailableSpeakerGate : ISpeakerGate
{
    public Task<SpeakerVerificationResult> VerifyCurrentUserAsync(
        AudioUtterance utterance,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SpeakerVerificationResult(
            Allowed: true,
            Score: 0f,
            VerificationSkipped: true));
}
