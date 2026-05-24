namespace ArrayMicRefreshment.Core;

/// <summary>Result of speaker embedding verification for one utterance.</summary>
public readonly record struct SpeakerVerificationResult(
    bool Allowed,
    float Score,
    bool VerificationSkipped);
