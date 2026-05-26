using System.Text.Json;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;
using Serilog;

namespace ArrayMicRefreshment.Speaker;

public sealed class UserEnrollmentService : IUserEnrollmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ISpeakerEmbeddingBackend _embeddingBackend;
    private readonly ISettingsStore _settingsStore;
    private readonly string _speakersDirectory;
    private AppSettings _settings;
    private readonly Dictionary<string, SpeakerEnrollmentRecord> _recordCache = new(StringComparer.Ordinal);

    public UserEnrollmentService(
        AppSettings settings,
        ISpeakerEmbeddingBackend embeddingBackend,
        ISettingsStore settingsStore,
        string? speakersDirectory = null)
    {
        _settings = settings;
        _embeddingBackend = embeddingBackend;
        _settingsStore = settingsStore;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _speakersDirectory = speakersDirectory
            ?? Path.Combine(appData, "ArrayMicRefreshment", "speakers");
        Directory.CreateDirectory(_speakersDirectory);
    }

    public string? CurrentUserId
    {
        get => _settings.CurrentSpeakerUserId;
        set
        {
            _settings.CurrentSpeakerUserId = value;
            _settingsStore.Save(_settings);
        }
    }

    public IReadOnlyList<SpeakerUserInfo> ListUsers()
    {
        if (!Directory.Exists(_speakersDirectory))
        {
            return Array.Empty<SpeakerUserInfo>();
        }

        return Directory.EnumerateFiles(_speakersDirectory, "*.json")
            .Select(LoadRecord)
            .Where(r => r is not null)
            .Select(r => new SpeakerUserInfo { UserId = r!.UserId, DisplayName = r.DisplayName })
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<EnrolledUser> ListEnrolledUsers() =>
        ListUsers().Select(u => new EnrolledUser(u.UserId, u.DisplayName)).ToArray();

    public void SetCurrentUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        CurrentUserId = userId;
    }

    public string AddUser(string name, IReadOnlyList<AudioUtterance> enrollmentUtterances)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Speaker name is required.", nameof(name));
        }

        if (enrollmentUtterances.Count < 3)
        {
            throw new ArgumentException("至少需要 3 段录音来注册说话人。", nameof(enrollmentUtterances));
        }

        // P0: Multi-template enrollment — compute embedding for each utterance
        var templates = new List<float[]>();
        foreach (var utterance in enrollmentUtterances)
        {
            var embedding = ComputeSingleEmbedding(utterance);
            templates.Add(embedding);
        }

        // P1: Z-Norm cohort stats — compute template-to-template score distribution
        var cohortScores = new List<float>();
        for (var i = 0; i < templates.Count; i++)
        {
            for (var j = i + 1; j < templates.Count; j++)
            {
                var score = SpeakerEmbeddingMath.CosineSimilarity(templates[i], templates[j]);
                cohortScores.Add(score);
            }
        }

        var (cohortMean, cohortStd) = SpeakerEmbeddingMath.ComputeMeanAndStd(
            cohortScores.Count > 0 ? cohortScores.ToArray() : new[] { 0.5f });

        // Legacy: compute mean embedding for backward compatibility
        var meanEmbedding = ComputeMeanEmbeddingFromTemplates(templates);

        var userId = Guid.NewGuid().ToString("N");
        var record = new SpeakerEnrollmentRecord
        {
            UserId = userId,
            DisplayName = name.Trim(),
            Embedding = meanEmbedding,
            Templates = templates,
            RecentScores = new List<float>(),
            CohortMean = cohortMean,
            CohortStd = cohortStd,
            EnrolledAt = DateTimeOffset.UtcNow,
        };

        SaveRecord(record);
        _recordCache[userId] = record;
        CurrentUserId = userId;
        Log.Information(
            "Enrolled speaker {Name} as {UserId} with {Count} templates, cohort μ={Mean:F3} σ={Std:F3}",
            record.DisplayName, userId, templates.Count, cohortMean, cohortStd);
        return userId;
    }

    public void DeleteUser(string userId)
    {
        var path = GetRecordPath(userId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        _recordCache.Remove(userId);

        if (string.Equals(CurrentUserId, userId, StringComparison.Ordinal))
        {
            CurrentUserId = null;
        }
    }

    public float[]? GetStoredEmbedding(string userId)
    {
        var record = GetRecord(userId);
        if (record?.Embedding is { Length: > 0 } embedding)
        {
            var normalized = (float[])embedding.Clone();
            SpeakerEmbeddingMath.L2NormalizeInPlace(normalized);
            return normalized;
        }

        return null;
    }

    /// <summary>P0: Get all enrollment templates for multi-template verification.</summary>
    public IReadOnlyList<float[]>? GetTemplates(string userId)
    {
        var record = GetRecord(userId);
        return record?.Templates?.Select(t => (float[])t.Clone()).ToArray();
    }

    /// <summary>P1: Get cohort stats for Z-Normalization.</summary>
    public (float Mean, float Std)? GetCohortStats(string userId)
    {
        var record = GetRecord(userId);
        if (record?.CohortMean is float mean && record?.CohortStd is float std)
        {
            return (mean, std);
        }
        return null;
    }

    /// <summary>P1: Get recent verification scores for adaptive threshold.</summary>
    public IReadOnlyList<float> GetRecentScores(string userId)
    {
        var record = GetRecord(userId);
        return record?.RecentScores?.ToArray() ?? Array.Empty<float>();
    }

    /// <summary>P1: Append a verification score to history (keeps last 20).</summary>
    public void AppendScore(string userId, float score)
    {
        var record = GetRecord(userId);
        if (record is null)
        {
            return;
        }

        var scores = record.RecentScores ?? new List<float>();
        scores.Add(score);
        // Keep last 20 scores
        while (scores.Count > 20)
        {
            scores.RemoveAt(0);
        }

        var updated = new SpeakerEnrollmentRecord
        {
            UserId = record.UserId,
            DisplayName = record.DisplayName,
            Embedding = record.Embedding,
            Templates = record.Templates,
            RecentScores = scores,
            CohortMean = record.CohortMean,
            CohortStd = record.CohortStd,
            EnrolledAt = record.EnrolledAt,
        };

        SaveRecord(updated);
        _recordCache[userId] = updated;
    }

    /// <summary>P1: Compute adaptive threshold based on user's historical scores.</summary>
    public float GetAdaptiveThreshold(string userId, float baseThreshold)
    {
        var scores = GetRecentScores(userId);
        if (scores.Count < 3)
        {
            // Not enough history — use base threshold with slight leniency
            return baseThreshold * 0.95f;
        }

        var median = SpeakerEmbeddingMath.Median(scores.ToArray());
        // Threshold = median * 0.80, clamped to [base * 0.70, base] so user setting is always the ceiling.
        var adaptive = median * 0.80f;
        var floor = baseThreshold * 0.70f;
        return Math.Min(Math.Max(adaptive, floor), baseThreshold);
    }

    private SpeakerEnrollmentRecord? GetRecord(string userId)
    {
        if (_recordCache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var record = LoadRecord(GetRecordPath(userId));
        if (record is not null)
        {
            _recordCache[userId] = record;
        }
        return record;
    }

    private float[] ComputeSingleEmbedding(AudioUtterance utterance)
    {
        // P2: Volume normalization before embedding computation
        var normalizedPcm = SpeakerEmbeddingMath.NormalizeVolume(utterance.Pcm16LeMono);
        var pcm = PcmConverters.Ensure16KHzMonoPcm16Le(normalizedPcm, utterance.SampleRate);
        var floats = PcmConverters.Pcm16LeToFloat(pcm);
        var embedding = _embeddingBackend.ComputeEmbedding(floats, PcmConverters.TargetSampleRate);
        SpeakerEmbeddingMath.L2NormalizeInPlace(embedding);
        return embedding;
    }

    private float[] ComputeMeanEmbeddingFromTemplates(List<float[]> templates)
    {
        if (templates.Count == 0)
        {
            throw new InvalidOperationException("No templates to compute mean embedding.");
        }

        var dim = templates[0].Length;
        var sum = new float[dim];
        foreach (var t in templates)
        {
            for (var i = 0; i < dim; i++)
            {
                sum[i] += t[i];
            }
        }

        for (var i = 0; i < dim; i++)
        {
            sum[i] /= templates.Count;
        }

        SpeakerEmbeddingMath.L2NormalizeInPlace(sum);
        return sum;
    }

    private string GetRecordPath(string userId) => Path.Combine(_speakersDirectory, $"{userId}.json");

    private void SaveRecord(SpeakerEnrollmentRecord record)
    {
        var json = JsonSerializer.Serialize(record, JsonOptions);
        File.WriteAllText(GetRecordPath(record.UserId), json);
    }

    private SpeakerEnrollmentRecord? LoadRecord(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SpeakerEnrollmentRecord>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load speaker record {Path}", path);
            return null;
        }
    }
}
