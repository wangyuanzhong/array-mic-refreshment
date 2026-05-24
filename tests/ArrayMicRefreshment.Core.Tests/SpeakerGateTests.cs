using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.Core.Tests;

public class SpeakerGateTests
{
    [Fact]
    public async Task No_enrollment_returns_true()
    {
        var backend = new FixedEmbeddingBackend(new float[] { 1f, 0f });
        var enrollment = new InMemoryEnrollmentService();
        var gate = new SpeakerGate(
            new AppSettings { SpeakerVerifyThreshold = 0.5f },
            enrollment,
            backend);

        var ok = await gate.VerifyCurrentUserAsync(CreateUtterance(), CancellationToken.None);
        Assert.True(ok.Allowed);
    }

    [Fact]
    public async Task Cosine_at_threshold_boundary()
    {
        var embedding = new float[] { 1f, 0f };
        var backend = new FixedEmbeddingBackend(embedding);
        var enrollment = new InMemoryEnrollmentService();
        var userId = enrollment.AddUser("tester", new[] { CreateUtterance() });
        enrollment.CurrentUserId = userId;
        enrollment.SetEmbedding(userId, embedding);

        var settings = new AppSettings { SpeakerVerifyThreshold = 1f };
        var gate = new SpeakerGate(settings, enrollment, backend);
        Assert.True((await gate.VerifyCurrentUserAsync(CreateUtterance(), CancellationToken.None)).Allowed);

        settings.SpeakerVerifyThreshold = 1.01f;
        var strictGate = new SpeakerGate(settings, enrollment, backend);
        Assert.False((await strictGate.VerifyCurrentUserAsync(CreateUtterance(), CancellationToken.None)).Allowed);
    }

    [Fact]
    public async Task Orthogonal_embedding_fails()
    {
        var backend = new FixedEmbeddingBackend(new float[] { 0f, 1f });
        var enrollment = new InMemoryEnrollmentService();
        var userId = enrollment.AddUser("tester", new[] { CreateUtterance() });
        enrollment.CurrentUserId = userId;
        enrollment.SetEmbedding(userId, new float[] { 1f, 0f });

        var gate = new SpeakerGate(new AppSettings { SpeakerVerifyThreshold = 0.5f }, enrollment, backend);
        var result = await gate.VerifyCurrentUserAsync(CreateUtterance(), CancellationToken.None);
        Assert.False(result.Allowed);
        Assert.True(result.Score < 0.5f);
    }

    private static AudioUtterance CreateUtterance() => new()
    {
        Pcm16LeMono = new byte[320],
        SampleRate = 16000,
        Duration = TimeSpan.FromMilliseconds(10),
    };

    private sealed class FixedEmbeddingBackend : ISpeakerEmbeddingBackend
    {
        private readonly float[] _embedding;

        public FixedEmbeddingBackend(float[] embedding) => _embedding = embedding;

        public int Dim => _embedding.Length;

        public float[] ComputeEmbedding(ReadOnlyMemory<float> samples, int sampleRate) => (float[])_embedding.Clone();

        public void Dispose()
        {
        }
    }

    private sealed class InMemoryEnrollmentService : IUserEnrollmentService
    {
        private readonly Dictionary<string, (string Name, float[] Embedding)> _users = new();

        public string? CurrentUserId { get; set; }

        public string AddUser(string name, IReadOnlyList<AudioUtterance> enrollmentUtterances)
        {
            var id = Guid.NewGuid().ToString("N");
            _users[id] = (name, Array.Empty<float>());
            return id;
        }

        public void SetEmbedding(string userId, float[] embedding) =>
            _users[userId] = (_users[userId].Name, embedding);

        public void DeleteUser(string userId) => _users.Remove(userId);

        public float[]? GetStoredEmbedding(string userId) =>
            _users.TryGetValue(userId, out var entry) ? entry.Embedding : null;

        public IReadOnlyList<SpeakerUserInfo> ListUsers() =>
            _users.Select(kv => new SpeakerUserInfo { UserId = kv.Key, DisplayName = kv.Value.Name }).ToArray();

        public IReadOnlyList<EnrolledUser> ListEnrolledUsers() =>
            ListUsers().Select(u => new EnrolledUser(u.UserId, u.DisplayName)).ToArray();

        public void SetCurrentUser(string userId) => CurrentUserId = userId;
    }
}
