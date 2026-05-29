using System.Text.Json;

namespace ArrayMicRefreshment.Core;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
}
