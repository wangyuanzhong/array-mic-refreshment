using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.App.Services;

/// <summary>Persist absolute models/skills directories so resolution does not depend on cwd.</summary>
public static class SettingsPathNormalizer
{
    public static void Normalize(AppSettings settings)
    {
        settings.ModelsDirectory = NormalizeModelsDirectory(settings.ModelsDirectory);
        settings.SkillsDirectory = NormalizeSkillsDirectory(settings.SkillsDirectory);
    }

    public static string NormalizeModelsDirectory(string? value)
    {
        var input = string.IsNullOrWhiteSpace(value) ? "models" : value.Trim();
        return Path.GetFullPath(ModelsPathResolver.Resolve(input));
    }

    public static string NormalizeSkillsDirectory(string? value)
    {
        var input = string.IsNullOrWhiteSpace(value) ? "skills" : value.Trim();
        return Path.GetFullPath(SkillsPathResolver.Resolve(input));
    }
}
