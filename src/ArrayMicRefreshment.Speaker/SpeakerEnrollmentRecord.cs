namespace ArrayMicRefreshment.Speaker;

public sealed class SpeakerEnrollmentRecord
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    
    /// <summary>Legacy single embedding (for backward compatibility).</summary>
    public float[]? Embedding { get; init; }
    
    /// <summary>Multiple enrollment templates (P0: multi-template enrollment).</summary>
    public List<float[]>? Templates { get; init; }
    
    /// <summary>Recent verification scores (P1: adaptive threshold).</summary>
    public List<float>? RecentScores { get; init; }
    
    /// <summary>Z-Norm cohort stats: mean of template-to-template scores (P1: Z-Norm).</summary>
    public float? CohortMean { get; init; }
    
    /// <summary>Z-Norm cohort stats: std dev of template-to-template scores (P1: Z-Norm).</summary>
    public float? CohortStd { get; init; }
    
    /// <summary>Timestamp of enrollment.</summary>
    public DateTimeOffset EnrolledAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class SpeakerUserInfo
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
}
