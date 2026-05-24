using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

public sealed record EnrolledUser(string Id, string Name)
{
    public static EnrolledUser None { get; } = new(string.Empty, "(无 — 不校验说话人)");

    public bool IsNone => string.IsNullOrEmpty(Id);
}

public interface IUserEnrollmentService
{
    string? CurrentUserId { get; set; }

    IReadOnlyList<SpeakerUserInfo> ListUsers();

    IReadOnlyList<EnrolledUser> ListEnrolledUsers();

    void SetCurrentUser(string userId);

    string AddUser(string name, IReadOnlyList<AudioUtterance> enrollmentUtterances);

    void DeleteUser(string userId);

    float[]? GetStoredEmbedding(string userId);
}
