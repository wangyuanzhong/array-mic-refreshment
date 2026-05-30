using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Read-only settings metadata queries shared by Web bridge.</summary>
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

    public sealed record WakeWordModelStatusEntry(
        string DisplayName,
        bool Installed,
        bool EngineReady,
        string ResolvedPath);

    public static WakeWordModelStatusEntry GetWakeWordModelStatus(
        string modelsDirectory,
        string? wakePhrase = null,
        WakeWordSensitivity sensitivity = WakeWordSensitivity.High)
    {
        var installed = WakeWordModelPaths.TryResolve(modelsDirectory, out var paths);
        var resolvedPath = installed
            ? Path.GetDirectoryName(paths!.TokensPath) ?? string.Empty
            : Path.Combine(ModelsPathResolver.Resolve(modelsDirectory), WakeWordModelPaths.ModelDirName);

        var engineReady = false;
        if (installed)
        {
            var modelRoot = Path.GetDirectoryName(paths!.TokensPath) ?? resolvedPath;
            WakeWordEncodingBootstrap.EnsureDefaultEncodings(modelRoot);
            var phrase = string.IsNullOrWhiteSpace(wakePhrase) ? "小助手" : wakePhrase.Trim();
            if (SherpaKeywordWakeWordDetector.TryCreate(modelsDirectory, phrase, out var probe, sensitivity)
                && probe is not null)
            {
                probe.Dispose();
                engineReady = true;
            }
        }

        return new WakeWordModelStatusEntry(
            WakeWordModelPaths.ModelDirName,
            installed,
            engineReady,
            resolvedPath);
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
        var list = new List<SpeakerUserEntry>
        {
            new(string.Empty, "无用户（不做声纹识别）", IsNone: true),
        };

        if (enrollment is null)
        {
            return list;
        }

        foreach (var u in enrollment.ListEnrolledUsers())
        {
            if (u.IsNone)
            {
                continue;
            }

            list.Add(new SpeakerUserEntry(u.Id, u.Name, IsNone: false));
        }

        return list;
    }
}
