using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public sealed class SettingsApplyServiceApplyTests
{
    [Fact]
    public void Apply_copies_incoming_into_target_and_rebuilds_pipeline_when_required()
    {
        var previous = new AppSettings { PromptRefineEnabled = false, PttHotkey = "Ctrl+Alt+Space" };
        var incoming = new AppSettings { PromptRefineEnabled = true, PttHotkey = "Ctrl+Alt+Space" };
        var target = SettingsApplyService.CloneSnapshot(previous);
        var host = new FakeSettingsApplyHost
        {
            TargetSettings = target,
            RegisteredPttHotkey = "Ctrl+Alt+Space",
        };

        var result = new SettingsApplyService().Apply(previous, incoming, host);

        Assert.True(result.PipelineRebuildRequired);
        Assert.True(host.RebuildPipelineCalled);
        Assert.False(host.ApplyPipelineSettingsCalled);
        Assert.True(incoming.PromptRefineEnabled);
        Assert.True(target.PromptRefineEnabled);
        Assert.True(host.InvalidateCaptureDeviceCalled);
        Assert.True(host.PersistAndRefreshCalled);
    }

    [Fact]
    public void Apply_applies_pipeline_settings_without_rebuild_when_only_non_pipeline_fields_change()
    {
        var previous = new AppSettings { MasterEnabled = true, PttHotkey = "Ctrl+Alt+Space" };
        var incoming = new AppSettings { MasterEnabled = false, PttHotkey = "Ctrl+Alt+Space" };
        var target = SettingsApplyService.CloneSnapshot(previous);
        var host = new FakeSettingsApplyHost
        {
            TargetSettings = target,
            RegisteredPttHotkey = "Ctrl+Alt+Space",
        };

        var result = new SettingsApplyService().Apply(previous, incoming, host);

        Assert.False(result.PipelineRebuildRequired);
        Assert.False(host.RebuildPipelineCalled);
        Assert.True(host.ApplyPipelineSettingsCalled);
        Assert.False(target.MasterEnabled);
    }

    [Fact]
    public void Apply_updates_wake_detector_and_capture_when_wake_fields_change()
    {
        var previous = new AppSettings
        {
            WakeWordPhrase = "小助手",
            WakeWordSensitivity = WakeWordSensitivity.Standard,
            WakeCommandSilenceMs = 3000,
            PttHotkey = "Ctrl+Alt+Space",
        };
        var incoming = new AppSettings
        {
            WakeWordPhrase = "你好",
            WakeWordSensitivity = WakeWordSensitivity.High,
            WakeCommandSilenceMs = 2500,
            PttHotkey = "Ctrl+Alt+Space",
        };
        var target = SettingsApplyService.CloneSnapshot(previous);
        var wakeDetector = new RecordingWakeWordDetector();
        var host = new FakeSettingsApplyHost
        {
            TargetSettings = target,
            RegisteredPttHotkey = "Ctrl+Alt+Space",
            WakeDetector = wakeDetector,
        };

        var result = new SettingsApplyService().Apply(previous, incoming, host);

        Assert.True(result.WakePhraseChanged);
        Assert.True(result.WakeSensitivityChanged);
        Assert.True(result.WakeCommandSilenceChanged);
        Assert.Equal("你好", wakeDetector.LastPhrase);
        Assert.Equal(WakeWordSensitivity.High, wakeDetector.LastSensitivity);
        Assert.Same(target, host.LastWakeCaptureSettings);
    }

    [Fact]
    public void Apply_sets_trigger_mode_and_refreshes_audio_when_wake_restart_required()
    {
        var previous = new AppSettings
        {
            TriggerMode = VoiceTriggerMode.PttOnly,
            SelectedDeviceId = "mic-a",
            PttHotkey = "Ctrl+Alt+Space",
        };
        var incoming = new AppSettings
        {
            TriggerMode = VoiceTriggerMode.Both,
            SelectedDeviceId = "mic-b",
            PttHotkey = "Ctrl+Alt+Space",
        };
        var target = SettingsApplyService.CloneSnapshot(previous);
        var host = new FakeSettingsApplyHost
        {
            TargetSettings = target,
            CurrentTriggerMode = VoiceTriggerMode.PttOnly,
            RegisteredPttHotkey = "Ctrl+Alt+Space",
        };

        var result = new SettingsApplyService().Apply(previous, incoming, host);

        Assert.True(result.TriggerModeChanged);
        Assert.True(result.WakeCaptureRestartRequired);
        Assert.Equal(VoiceTriggerMode.Both, host.LastTriggerMode);
        Assert.True(host.RefreshAudioCaptureAfterSettingsCalled);
    }

    [Fact]
    public void Apply_records_wake_phrase_error_without_throwing()
    {
        var previous = new AppSettings { WakeWordPhrase = "小助手", PttHotkey = "Ctrl+Alt+Space" };
        var incoming = new AppSettings { WakeWordPhrase = "bad", PttHotkey = "Ctrl+Alt+Space" };
        var target = SettingsApplyService.CloneSnapshot(previous);
        var wakeDetector = new RecordingWakeWordDetector { ThrowOnApplyPhrase = true };
        var host = new FakeSettingsApplyHost
        {
            TargetSettings = target,
            RegisteredPttHotkey = "Ctrl+Alt+Space",
            WakeDetector = wakeDetector,
        };

        var result = new SettingsApplyService().Apply(previous, incoming, host);

        Assert.True(result.WakePhraseChanged);
        Assert.Equal("boom", result.WakePhraseError);
        Assert.Equal("唤醒词配置更新失败：boom", host.LastWakePhraseWarning);
    }

    [Fact]
    public void Apply_calls_host_hotkey_update_when_hotkey_changes_even_with_stub_ptt()
    {
        var previous = new AppSettings { PttHotkey = "Ctrl+Alt+Space" };
        var incoming = new AppSettings { PttHotkey = "F8" };
        var target = SettingsApplyService.CloneSnapshot(previous);
        var host = new FakeSettingsApplyHost
        {
            TargetSettings = target,
            RegisteredPttHotkey = "Ctrl+Alt+Space",
            PushToTalk = new StubPushToTalkSource(),
        };

        var result = new SettingsApplyService().Apply(previous, incoming, host);

        Assert.True(result.HotkeyChanged);
        Assert.True(result.HotkeyUpdateAttempted);
        Assert.True(result.HotkeyUpdateSucceeded);
        Assert.True(host.TryUpdatePttHotkeyCalled);
        Assert.Equal("F8", host.RegisteredPttHotkey);
    }

    private sealed class FakeSettingsApplyHost : ISettingsApplyHost
    {
        public required AppSettings TargetSettings { get; init; }

        public VoiceTriggerMode CurrentTriggerMode { get; init; } = VoiceTriggerMode.PttOnly;

        public string? RegisteredPttHotkey { get; set; }

        public IPushToTalkSource PushToTalk { get; init; } = new StubPushToTalkSource();

        public IWakeWordDetector WakeDetector { get; init; } = new RecordingWakeWordDetector();

        public bool RebuildPipelineCalled { get; private set; }

        public bool ApplyPipelineSettingsCalled { get; private set; }

        public bool InvalidateCaptureDeviceCalled { get; private set; }

        public bool PersistAndRefreshCalled { get; private set; }

        public bool RefreshAudioCaptureAfterSettingsCalled { get; private set; }

        public bool TryUpdatePttHotkeyCalled { get; private set; }

        public VoiceTriggerMode? LastTriggerMode { get; private set; }

        public AppSettings? LastWakeCaptureSettings { get; private set; }

        public HudScreenCorner? LastHudCorner { get; private set; }

        public bool? LastLaunchAtStartup { get; private set; }

        public string? LastWakePhraseWarning { get; private set; }

        public void RebuildPipeline() => RebuildPipelineCalled = true;

        public void ApplyPipelineSettings() => ApplyPipelineSettingsCalled = true;

        public bool TryUpdatePttHotkey(string hotkey, out string? error)
        {
            TryUpdatePttHotkeyCalled = true;
            error = null;
            return true;
        }

        public void NotifyPttHotkeyUpdated(string hotkeyDisplay)
        {
        }

        public void NotifyPttHotkeyFailed(string? error)
        {
        }

        public void SetVoiceTriggerMode(VoiceTriggerMode mode) => LastTriggerMode = mode;

        public void ApplyWakeCaptureSettings(AppSettings settings) => LastWakeCaptureSettings = settings;

        public void ApplyHudCorner(HudScreenCorner corner) => LastHudCorner = corner;

        public void ApplyLaunchAtStartup(bool enabled) => LastLaunchAtStartup = enabled;

        public void InvalidateCaptureDevice() => InvalidateCaptureDeviceCalled = true;

        public void PersistAndRefresh() => PersistAndRefreshCalled = true;

        public void RefreshAudioCaptureAfterSettings() => RefreshAudioCaptureAfterSettingsCalled = true;

        public void ShowWakePhraseWarning(string message) => LastWakePhraseWarning = message;
    }

    private sealed class StubPushToTalkSource : IPushToTalkSource
    {
#pragma warning disable CS0067
        public event EventHandler? PttPressed;

        public event EventHandler? PttReleased;
#pragma warning restore CS0067

        public string HotkeyDisplay => "Stub";
    }

    private sealed class RecordingWakeWordDetector : IWakeWordDetector
    {
        public string DetectorId => "recording";

        public bool IsRunning { get; private set; }

        public bool ThrowOnApplyPhrase { get; init; }

        public string? LastPhrase { get; private set; }

        public WakeWordSensitivity? LastSensitivity { get; private set; }

        public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

        public void ApplyPhrase(string phrase)
        {
            if (ThrowOnApplyPhrase)
            {
                throw new InvalidOperationException("boom");
            }

            LastPhrase = phrase;
        }

        public void ApplyWakeSensitivity(WakeWordSensitivity sensitivity) => LastSensitivity = sensitivity;

        public void Dispose()
        {
        }

        public void ProcessAudio(ReadOnlySpan<short> pcm16Mono, int sampleRate)
        {
        }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;
    }
}
