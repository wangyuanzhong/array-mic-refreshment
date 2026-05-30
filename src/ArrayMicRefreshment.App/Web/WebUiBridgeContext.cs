using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Dependencies injected into <see cref="WebUiBridge"/> per host window session.</summary>
public sealed class WebUiBridgeContext
{
    public required AppSettings Settings { get; init; }

    public required ISettingsStore SettingsStore { get; init; }

    public IUserEnrollmentService? Enrollment { get; init; }

    public IEnrollmentUtteranceSource? EnrollmentCapture { get; init; }

    /// <summary>Dispose when the host WebView closes if <see cref="EnrollmentCapture"/> was created for settings/enroll routes.</summary>
    public IDisposable? EnrollmentCaptureLifetime { get; init; }

    /// <summary>Live tray trigger mode override (shown in Web settings when runtime mode differs from persisted).</summary>
    public VoiceTriggerMode? RuntimeTriggerMode { get; set; }

    /// <summary>Live master enable flag from tray runtime.</summary>
    public bool? MasterEnabled { get; set; }

    public IAudioDeviceEnumerator? DeviceEnumerator { get; init; }

    public bool SpeakerModelMissing { get; set; }

    /// <summary>When set, <see cref="WebUiBridge.SaveSettingsDraft"/> uses <see cref="SettingsApplyService"/>.</summary>
    public ISettingsApplyHost? SettingsApplyHost { get; init; }

    public SettingsApplyService? SettingsApplyService { get; init; }

    /// <summary>Tray UI thread for dialogs when settings WebView is hidden.</summary>
    public SynchronizationContext? UiSynchronizationContext { get; init; }

    /// <summary>Called on UI thread when a Web route completes successfully (e.g. enrollment).</summary>
    public Action? OnSuccess { get; init; }

    internal WebUiHostForm? HostForm { get; set; }
}
