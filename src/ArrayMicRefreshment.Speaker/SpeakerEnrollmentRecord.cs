namespace ArrayMicRefreshment.Speaker;

public sealed class SpeakerEnrollmentRecord
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public required float[] Embedding { get; init; }
}

public sealed class SpeakerUserInfo
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
}
