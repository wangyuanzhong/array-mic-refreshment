using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Builds a <see cref="WebUiBridgeContext"/> for the settings route (integration wiring helper).</summary>
public static class WebUiSettingsContextFactory
{
    public static WebUiBridgeContext Create(
        AppSettings settings,
        ISettingsStore settingsStore,
        ISettingsApplyHost settingsApplyHost,
        VoiceTriggerMode runtimeTriggerMode,
        bool masterEnabled,
        IAudioDeviceEnumerator? deviceEnumerator = null,
        IUserEnrollmentService? enrollment = null,
        bool speakerModelMissing = false,
        SettingsApplyService? settingsApplyService = null)
    {
        return new WebUiBridgeContext
        {
            Settings = settings,
            SettingsStore = settingsStore,
            SettingsApplyHost = settingsApplyHost,
            SettingsApplyService = settingsApplyService ?? new SettingsApplyService(),
            RuntimeTriggerMode = runtimeTriggerMode,
            MasterEnabled = masterEnabled,
            DeviceEnumerator = deviceEnumerator,
            Enrollment = enrollment,
            SpeakerModelMissing = speakerModelMissing,
        };
    }
}
