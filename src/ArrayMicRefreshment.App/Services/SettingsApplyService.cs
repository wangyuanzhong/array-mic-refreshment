using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.App.Services;

/// <summary>Describes which settings-driven side effects are required after a save.</summary>
public sealed record SettingsApplyResult
{
    public bool PipelineRebuildRequired { get; init; }
    public bool HotkeyChanged { get; init; }
    public bool HotkeyUpdateAttempted { get; init; }
    public bool HotkeyUpdateSucceeded { get; init; }
    public bool TriggerModeChanged { get; init; }
    public bool WakePhraseChanged { get; init; }
    public bool WakeSensitivityChanged { get; init; }
    public bool WakeCommandSilenceChanged { get; init; }
    public bool HudCornerChanged { get; init; }
    public bool LaunchAtStartupChanged { get; init; }
    public bool WakeCaptureRestartRequired { get; init; }
    public string? HotkeyError { get; init; }
    public string? WakePhraseError { get; init; }
}

/// <summary>Host-provided runtime hooks for applying persisted settings (tray, audio, pipeline).</summary>
public interface ISettingsApplyHost
{
    AppSettings TargetSettings { get; }

    VoiceTriggerMode CurrentTriggerMode { get; }

    string? RegisteredPttHotkey { get; set; }

    void RebuildPipeline();

    void ApplyPipelineSettings();

    IPushToTalkSource PushToTalk { get; }

    bool TryUpdatePttHotkey(string hotkey, out string? error);

    void NotifyPttHotkeyUpdated(string hotkeyDisplay);

    void NotifyPttHotkeyFailed(string? error);

    IWakeWordDetector WakeDetector { get; }

    void ApplyWakeCaptureSettings(AppSettings settings);

    void SetVoiceTriggerMode(VoiceTriggerMode mode);

    void ApplyHudCorner(HudScreenCorner corner);

    void ApplyLaunchAtStartup(bool enabled);

    void InvalidateCaptureDevice();

    void PersistAndRefresh();

    void RefreshAudioCaptureAfterSettings();

    void ShowWakePhraseWarning(string message);
}

public sealed class SettingsApplyService
{
    public SettingsApplyResult Apply(AppSettings previous, AppSettings incoming, ISettingsApplyHost host)
    {
        SettingsCopier.CopyInto(incoming, host.TargetSettings);
        var current = host.TargetSettings;

        var result = ComputeChanges(
            previous,
            current,
            host.CurrentTriggerMode,
            host.RegisteredPttHotkey);

        Log.Information(
            "[DIAGNOSTIC] Settings changed. PreviousRefine={PrevRefine}, CurrentRefine={CurrRefine}, " +
            "RebuildRequired={Rebuild}",
            previous.PromptRefineEnabled,
            current.PromptRefineEnabled,
            result.PipelineRebuildRequired);

        if (result.PipelineRebuildRequired)
        {
            Log.Information("Pipeline rebuild required. Rebuilding...");
            host.RebuildPipeline();
        }
        else
        {
            host.ApplyPipelineSettings();
        }

        if (result.HotkeyChanged)
        {
            result = result with
            {
                HotkeyUpdateAttempted = true,
            };

            if (!host.TryUpdatePttHotkey(current.PttHotkey, out var hotkeyError))
            {
                host.NotifyPttHotkeyFailed(hotkeyError);
                result = result with
                {
                    HotkeyUpdateSucceeded = false,
                    HotkeyError = hotkeyError,
                };
            }
            else
            {
                host.RegisteredPttHotkey = current.PttHotkey;
                host.NotifyPttHotkeyUpdated(host.PushToTalk.HotkeyDisplay);
                result = result with { HotkeyUpdateSucceeded = true };
            }
        }

        if (result.TriggerModeChanged)
        {
            host.SetVoiceTriggerMode(NormalizePersistedMode(current.TriggerMode));
        }

        if (result.WakePhraseChanged)
        {
            try
            {
                host.WakeDetector.ApplyPhrase(current.WakeWordPhrase);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to apply wake phrase after settings save");
                host.ShowWakePhraseWarning($"唤醒词配置更新失败：{ex.Message}");
                result = result with { WakePhraseError = ex.Message };
            }
        }

        if (result.WakeSensitivityChanged)
        {
            host.WakeDetector.ApplyWakeSensitivity(current.WakeWordSensitivity);
        }

        if (result.WakeCommandSilenceChanged)
        {
            host.ApplyWakeCaptureSettings(current);
        }

        if (result.HudCornerChanged)
        {
            host.ApplyHudCorner(current.HudScreenCorner);
        }

        if (result.LaunchAtStartupChanged)
        {
            host.ApplyLaunchAtStartup(current.LaunchAtStartup);
        }

        host.InvalidateCaptureDevice();
        host.PersistAndRefresh();

        if (result.WakeCaptureRestartRequired)
        {
            host.RefreshAudioCaptureAfterSettings();
        }

        return result;
    }

    public static SettingsApplyResult ComputeChanges(
        AppSettings previous,
        AppSettings current,
        VoiceTriggerMode currentTriggerMode,
        string? registeredPttHotkey)
    {
        var pipelineRebuildRequired = SettingsCopier.RequiresPipelineRebuild(previous, current);
        var hotkeyChanged = !string.Equals(
            registeredPttHotkey,
            current.PttHotkey,
            StringComparison.OrdinalIgnoreCase);
        var persistedMode = NormalizePersistedMode(current.TriggerMode);
        var triggerModeChanged = currentTriggerMode != persistedMode;

        return new SettingsApplyResult
        {
            PipelineRebuildRequired = pipelineRebuildRequired,
            HotkeyChanged = hotkeyChanged,
            TriggerModeChanged = triggerModeChanged,
            WakePhraseChanged = !string.Equals(
                previous.WakeWordPhrase,
                current.WakeWordPhrase,
                StringComparison.Ordinal),
            WakeSensitivityChanged = previous.WakeWordSensitivity != current.WakeWordSensitivity,
            WakeCommandSilenceChanged = previous.WakeCommandSilenceMs != current.WakeCommandSilenceMs,
            HudCornerChanged = previous.HudScreenCorner != current.HudScreenCorner,
            LaunchAtStartupChanged = previous.LaunchAtStartup != current.LaunchAtStartup,
            WakeCaptureRestartRequired = SettingsCopier.RequiresWakeCaptureRestart(previous, current),
        };
    }

    public static AppSettings CloneSnapshot(AppSettings source)
    {
        var clone = new AppSettings();
        SettingsCopier.CopyInto(source, clone);
        return clone;
    }

    public static VoiceTriggerMode NormalizePersistedMode(VoiceTriggerMode mode) =>
        mode switch
        {
            VoiceTriggerMode.WakeWordOnly => VoiceTriggerMode.WakeWordOnly,
            VoiceTriggerMode.Both => VoiceTriggerMode.Both,
            _ => VoiceTriggerMode.PttOnly,
        };
}
