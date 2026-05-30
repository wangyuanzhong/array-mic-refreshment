using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArrayMicRefreshment.Core;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _path;

    public JsonSettingsStore(string? path = null)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(root, "ArrayMicRefreshment");
        Directory.CreateDirectory(dir);
        _path = path ?? Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var settings = new AppSettings();
            settings.MigrateLegacyApiSettings();
            settings.MigrateLegacyFeaturePresets();
            return settings;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (!settings.PasteToCaretEnabled)
            {
                settings.PasteToCaretEnabled = true;
            }

            if (!json.Contains("launchAtStartup", StringComparison.OrdinalIgnoreCase))
            {
                settings.LaunchAtStartup = true;
            }

            settings.MigrateLegacyApiSettings();
            settings.MigrateLegacyFeaturePresets();
            MigrateLegacyPttRecordingMode(json, settings);
            return settings;
        }
        catch
        {
            var settings = new AppSettings();
            settings.MigrateLegacyApiSettings();
            settings.MigrateLegacyFeaturePresets();
            return settings;
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }

    /// <summary>V0.4.30 stored toggle under <c>pttRecordingMode</c>; that is now <see cref="VoiceTriggerMode.Manual"/>.</summary>
    private static void MigrateLegacyPttRecordingMode(string json, AppSettings settings)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("pttRecordingMode", out var modeProp))
            {
                return;
            }

            var isToggle = modeProp.ValueKind switch
            {
                JsonValueKind.String => string.Equals(modeProp.GetString(), "Toggle", StringComparison.OrdinalIgnoreCase),
                JsonValueKind.Number => modeProp.GetInt32() == 1,
                _ => false,
            };
            if (isToggle && settings.TriggerMode == VoiceTriggerMode.PttOnly)
            {
                settings.TriggerMode = VoiceTriggerMode.Manual;
            }
        }
        catch
        {
            // Best-effort migration only.
        }
    }
}
