using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

public sealed record EnrolledUser(string Id, string Name);

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
