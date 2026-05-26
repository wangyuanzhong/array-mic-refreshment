using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;
using Serilog;

namespace ArrayMicRefreshment.Speaker;

public sealed class SpeakerGate : ISpeakerGate, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IUserEnrollmentService _enrollment;
    private readonly ISpeakerEmbeddingBackend _embeddingBackend;

    public const int SlidingWindowSize = 3;

    // P0: Sliding window for multi-utterance verification
    private readonly Queue<float> _recentScores = new();

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

    private UserEnrollmentService? TryGetUserEnrollmentService() =>
        _enrollment as UserEnrollmentService;

    public Task<SpeakerVerificationResult> VerifyCurrentUserAsync(
        AudioUtterance utterance,
        CancellationToken cancellationToken)
    {
        var userId = _enrollment.CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            Log.Warning("Speaker gate: no CurrentSpeakerUserId configured; allowing utterance.");
            return Task.FromResult(new SpeakerVerificationResult(true, 0f, VerificationSkipped: true));
        }

        var userSvc = TryGetUserEnrollmentService();
        var templates = userSvc?.GetTemplates(userId);
        if (templates is null || templates.Count == 0)
        {
            // Fallback to legacy single embedding
            var stored = _enrollment.GetStoredEmbedding(userId);
            if (stored is null || stored.Length == 0)
            {
                Log.Warning(
                    "Speaker gate: no enrollment data for user {UserId}; allowing utterance.",
                    userId);
                return Task.FromResult(new SpeakerVerificationResult(true, 0f, VerificationSkipped: true));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () =>
            {
                // P2: Volume normalization before embedding extraction
                var speakerPcm = utterance.SpeakerVerifyPcm16LeMono ?? utterance.Pcm16LeMono;
                if (utterance.SpeakerVerifyPcm16LeMono is not null)
                {
                    Log.Debug(
                        "Speaker gate using trimmed verify PCM ({Bytes} bytes, full={FullBytes})",
                        speakerPcm.Length,
                        utterance.Pcm16LeMono.Length);
                }

                var normalizedPcm = SpeakerEmbeddingMath.NormalizeVolume(speakerPcm);
                var pcm = PcmConverters.Ensure16KHzMonoPcm16Le(normalizedPcm, utterance.SampleRate);
                var floats = PcmConverters.Pcm16LeToFloat(pcm);
                var probe = _embeddingBackend.ComputeEmbedding(floats, PcmConverters.TargetSampleRate);
                SpeakerEmbeddingMath.L2NormalizeInPlace(probe);

                // P0: Multi-template verification — compare against all templates, take top-2 average
                float rawScore;
                if (templates is not null && templates.Count > 0)
                {
                    var scores = templates
                        .Select(t => SpeakerEmbeddingMath.CosineSimilarity(probe, t))
                        .OrderByDescending(s => s)
                        .ToArray();

                    // Take top-2 average (or top-1 if only 1 template)
                    var topCount = Math.Min(2, scores.Length);
                    rawScore = scores.Take(topCount).Average();
                    Log.Debug(
                        "Multi-template scores for {UserId}: top={Top:F3} second={Second:F3} raw={Raw:F3}",
                        userId,
                        scores[0],
                        scores.Length > 1 ? scores[1] : scores[0],
                        rawScore);
                }
                else
                {
                    // Legacy fallback
                    var stored = _enrollment.GetStoredEmbedding(userId)!;
                    rawScore = SpeakerEmbeddingMath.CosineSimilarity(probe, stored);
                }

                // P1: Z-Normalization
                var zScore = rawScore;
                var cohortStats = userSvc?.GetCohortStats(userId);
                if (cohortStats is (float mean, float std))
                {
                    zScore = SpeakerEmbeddingMath.ZNormalize(rawScore, mean, std);
                    Log.Debug(
                        "Z-Norm for {UserId}: raw={Raw:F3} cohort(μ={Mean:F3},σ={Std:F3}) z={Z:F3}",
                        userId, rawScore, mean, std, zScore);
                }

                // P1: Adaptive threshold
                var baseThreshold = _settings.SpeakerVerifyThreshold;
                var adaptiveThreshold = userSvc?.GetAdaptiveThreshold(userId, baseThreshold) ?? baseThreshold;
                var lenientThreshold = adaptiveThreshold * 0.85f;

                // P0: Sliding window — only keep recent attempts
                _recentScores.Enqueue(rawScore);
                while (_recentScores.Count > SlidingWindowSize)
                {
                    _recentScores.Dequeue();
                }

                var windowAverage = _recentScores.Average();

                // Dual-track decision:
                // 1. Current utterance is good enough → pass immediately
                // 2. Current is slightly below, but recent average is still okay → lenient pass
                // 3. Both current and average are low → reject
                var pass = rawScore >= adaptiveThreshold ||
                           (rawScore >= lenientThreshold && windowAverage >= adaptiveThreshold);

                if (pass)
                {
                    // Record successful score for future threshold adaptation
                    userSvc?.AppendScore(userId, rawScore);
                    Log.Information(
                        "Speaker verify user {UserId}: raw={Raw:F3} z={Z:F3} window={Window:F3} " +
                        "threshold={Th:F3}(lenient={Lenient:F3}) pass=true",
                        userId, rawScore, zScore, windowAverage, adaptiveThreshold, lenientThreshold);
                }
                else
                {
                    Log.Warning(
                        "Speaker gate rejected user {UserId}: raw={Raw:F3} z={Z:F3} window={Window:F3} " +
                        "threshold={Th:F3}(lenient={Lenient:F3}) scores=[{Scores}]",
                        userId, rawScore, zScore, windowAverage, adaptiveThreshold, lenientThreshold,
                        string.Join(",", _recentScores.Select(s => $"{s:F3}")));
                }

                return new SpeakerVerificationResult(
                    pass,
                    rawScore,
                    VerificationSkipped: false,
                    EffectiveThreshold: adaptiveThreshold,
                    WindowAverage: windowAverage);
            },
            cancellationToken);
    }

    public void Dispose() => _embeddingBackend.Dispose();
}
