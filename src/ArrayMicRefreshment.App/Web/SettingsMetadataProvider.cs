using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Read-only settings metadata queries shared by Web bridge and WinForms.</summary>
public static class SettingsMetadataProvider
{
    public sealed record AudioDeviceEntry(string Id, string DisplayName, bool IsDefault);

    public sealed record AsrModelEntry(string Id, string DisplayName, bool Installed);

    public sealed record OptionalOverlaySkillEntry(string Key, string Label, bool Checked);

    public static IReadOnlyList<AudioDeviceEntry> ListAudioDevices(IAudioDeviceEnumerator? enumerator)
    {
        if (enumerator is null)
        {
            return Array.Empty<AudioDeviceEntry>();
        }

        return DeviceComboPopulator.BuildItems(enumerator)
            .Select(item => new AudioDeviceEntry(item.Id, item.DisplayName, item.Device.IsDefault))
            .ToArray();
    }

    public static IReadOnlyList<AsrModelEntry> ListAsrModels(string modelsDirectory)
    {
        var installedIds = SenseVoiceModelResolver.ListAvailableModels(modelsDirectory)
            .Select(m => m.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return AsrModelInfo.All
            .Select(m => new AsrModelEntry(m.Id, m.DisplayName, installedIds.Contains(m.Id)))
            .ToArray();
    }

    public static IReadOnlyList<OptionalOverlaySkillEntry> ListOptionalOverlaySkills(
        string skillsDirectory,
        IEnumerable<string> checkedKeys)
    {
        var checkedSet = checkedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(skillsDirectory));
            return catalog.OptionalSkills
                .Select(entry => new OptionalOverlaySkillEntry(
                    entry.Key,
                    string.IsNullOrWhiteSpace(entry.Note) ? entry.Key : entry.Note!,
                    checkedSet.Contains(entry.Key)))
                .ToArray();
        }
        catch
        {
            return Array.Empty<OptionalOverlaySkillEntry>();
        }
    }

    public static IReadOnlyList<string> GetSkillsCatalogMissingFiles(string skillsDirectory)
    {
        try
        {
            var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(skillsDirectory));
            return catalog.MissingFiles;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public sealed record SpeakerUserEntry(string Id, string DisplayName, bool IsNone);

    public static IReadOnlyList<SpeakerUserEntry> ListSpeakerUsers(IUserEnrollmentService? enrollment)
    {
        if (enrollment is null)
        {
            return Array.Empty<SpeakerUserEntry>();
        }

        return enrollment.ListEnrolledUsers()
            .Select(u => new SpeakerUserEntry(u.Id, u.Name, u.IsNone))
            .ToArray();
    }
}
