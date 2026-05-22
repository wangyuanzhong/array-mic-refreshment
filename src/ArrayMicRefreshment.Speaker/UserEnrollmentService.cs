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

        if (enrollmentUtterances.Count == 0)
        {
            throw new ArgumentException("At least one enrollment utterance is required.", nameof(enrollmentUtterances));
        }

        var embedding = ComputeMeanEmbedding(enrollmentUtterances);
        var userId = Guid.NewGuid().ToString("N");
        var record = new SpeakerEnrollmentRecord
        {
            UserId = userId,
            DisplayName = name.Trim(),
            Embedding = embedding,
        };

        SaveRecord(record);
        CurrentUserId = userId;
        Log.Information("Enrolled speaker {Name} as {UserId}", record.DisplayName, userId);
        return userId;
    }

    public void DeleteUser(string userId)
    {
        var path = GetRecordPath(userId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        if (string.Equals(CurrentUserId, userId, StringComparison.Ordinal))
        {
            CurrentUserId = null;
        }
    }

    public float[]? GetStoredEmbedding(string userId)
    {
        var record = LoadRecord(GetRecordPath(userId));
        return record?.Embedding;
    }

    private float[] ComputeMeanEmbedding(IReadOnlyList<AudioUtterance> utterances)
    {
        float[]? sum = null;
        var count = 0;

        foreach (var utterance in utterances)
        {
            var pcm = PcmConverters.Ensure16KHzMonoPcm16Le(utterance.Pcm16LeMono, utterance.SampleRate);
            var floats = PcmConverters.Pcm16LeToFloat(pcm);
            var embedding = _embeddingBackend.ComputeEmbedding(floats, PcmConverters.TargetSampleRate);

            if (sum is null)
            {
                sum = (float[])embedding.Clone();
            }
            else
            {
                for (var i = 0; i < sum.Length; i++)
                {
                    sum[i] += embedding[i];
                }
            }

            count++;
        }

        if (sum is null || count == 0)
        {
            throw new InvalidOperationException("Failed to compute enrollment embedding.");
        }

        for (var i = 0; i < sum.Length; i++)
        {
            sum[i] /= count;
        }

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
