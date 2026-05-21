using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

public interface IUserEnrollmentService
{
    string? CurrentUserId { get; set; }

    IReadOnlyList<SpeakerUserInfo> ListUsers();

    string AddUser(string name, IReadOnlyList<AudioUtterance> enrollmentUtterances);

    void DeleteUser(string userId);

    float[]? GetStoredEmbedding(string userId);
}
