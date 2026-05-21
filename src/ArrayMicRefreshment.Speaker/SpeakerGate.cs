using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;
using Serilog;

namespace ArrayMicRefreshment.Speaker;

public sealed class SpeakerGate : ISpeakerGate, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IUserEnrollmentService _enrollment;
    private readonly ISpeakerEmbeddingBackend _embeddingBackend;

    public SpeakerGate(
        AppSettings settings,
        IUserEnrollmentService enrollment,
        ISpeakerEmbeddingBackend embeddingBackend)
    {
        _settings = settings;
        _enrollment = enrollment;
        _embeddingBackend = embeddingBackend;
    }

    public static SpeakerGate CreateFromSettings(AppSettings settings, ISettingsStore settingsStore)
    {
        var paths = SpeakerModelResolver.Resolve(settings.ModelsDirectory);
        var backend = new SherpaSpeakerEmbeddingBackend(paths);
        var enrollment = new UserEnrollmentService(settings, backend, settingsStore);
        return new SpeakerGate(settings, enrollment, backend);
    }

    public IUserEnrollmentService Enrollment => _enrollment;

    public Task<bool> VerifyCurrentUserAsync(AudioUtterance utterance, CancellationToken cancellationToken)
    {
        var userId = _enrollment.CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            Log.Warning("Speaker gate: no CurrentSpeakerUserId configured; allowing utterance.");
            return Task.FromResult(true);
        }

        var stored = _enrollment.GetStoredEmbedding(userId);
        if (stored is null || stored.Length == 0)
        {
            Log.Warning(
                "Speaker gate: no enrollment embedding for user {UserId}; allowing utterance.",
                userId);
            return Task.FromResult(true);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () =>
            {
                var pcm = PcmConverters.Ensure16KHzMonoPcm16Le(utterance.Pcm16LeMono, utterance.SampleRate);
                var floats = PcmConverters.Pcm16LeToFloat(pcm);
                var probe = _embeddingBackend.ComputeEmbedding(floats, PcmConverters.TargetSampleRate);
                var score = SpeakerEmbeddingMath.CosineSimilarity(probe, stored);
                var pass = score >= _settings.SpeakerVerifyThreshold;
                Log.Debug(
                    "Speaker verify user {UserId}: score={Score:F3} threshold={Threshold:F3} pass={Pass}",
                    userId,
                    score,
                    _settings.SpeakerVerifyThreshold,
                    pass);
                return pass;
            },
            cancellationToken);
    }

    public void Dispose() => _embeddingBackend.Dispose();
}
