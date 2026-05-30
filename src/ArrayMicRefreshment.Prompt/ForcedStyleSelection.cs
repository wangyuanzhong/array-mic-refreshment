using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Prompt;

/// <summary>Maps between settings <c>forcedSpecialistKey</c> and legacy <see cref="PromptIntent"/>.</summary>
public static class ForcedStyleSelection
{
    public const string AutoKey = "auto";

    public static string GetEffectiveKey(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ForcedSpecialistKey))
        {
            return settings.ForcedSpecialistKey.Trim();
        }

        return settings.ForcedIntent == PromptIntent.Auto
            ? AutoKey
            : SpecialistKeyMapper.ToSpecialistKey(settings.ForcedIntent);
    }

    public static void ApplyKey(AppSettings settings, string? specialistKey)
    {
        var key = string.IsNullOrWhiteSpace(specialistKey)
            ? AutoKey
            : specialistKey.Trim();

        settings.ForcedSpecialistKey = key;
        settings.ForcedIntent = key switch
        {
            AutoKey => PromptIntent.Auto,
            _ => SpecialistKeyMapper.FromSpecialistKey(key),
        };
    }

    public static void ApplyKey(FeaturePreset preset, string? specialistKey)
    {
        var key = string.IsNullOrWhiteSpace(specialistKey)
            ? AutoKey
            : specialistKey.Trim();

        preset.ForcedSpecialistKey = key;
        preset.ForcedIntent = key switch
        {
            AutoKey => PromptIntent.Auto,
            _ => SpecialistKeyMapper.FromSpecialistKey(key),
        };
    }

    public static string ResolvePresetKey(FeaturePreset preset) =>
        !string.IsNullOrWhiteSpace(preset.ForcedSpecialistKey)
            ? preset.ForcedSpecialistKey.Trim()
            : preset.ForcedIntent == PromptIntent.Auto
                ? AutoKey
                : SpecialistKeyMapper.ToSpecialistKey(preset.ForcedIntent);

    public static void MigrateAppSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ForcedSpecialistKey))
        {
            ApplyKey(settings, settings.ForcedIntent == PromptIntent.Auto
                ? AutoKey
                : SpecialistKeyMapper.ToSpecialistKey(settings.ForcedIntent));
        }

        if (settings.FeaturePresets is null)
        {
            return;
        }

        foreach (var preset in settings.FeaturePresets)
        {
            if (string.IsNullOrWhiteSpace(preset.ForcedSpecialistKey))
            {
                ApplyKey(preset, preset.ForcedIntent == PromptIntent.Auto
                    ? AutoKey
                    : SpecialistKeyMapper.ToSpecialistKey(preset.ForcedIntent));
            }
        }
    }
}
