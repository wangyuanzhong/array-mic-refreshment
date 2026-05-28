using System.Text.Json;
using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

/// <summary>Shared helpers for Route B Phase 2 automated acceptance (docs/UI_ROUTE_B_WEBVIEW2.md §8).</summary>
internal static class Phase2AcceptanceTestSupport
{
    internal static readonly JsonSerializerOptions DraftJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>§7.3 draft JSON property names the Web UI must round-trip.</summary>
    internal static readonly string[] DraftJsonPropertyNames =
    [
        "masterEnabled",
        "pasteToCaretEnabled",
        "launchAtStartup",
        "promptRefineEnabled",
        "forcedIntent",
        "onRefineFailure",
        "selectedDeviceId",
        "currentSpeakerUserId",
        "speakerVerifyThreshold",
        "selectedAsrModelId",
        "skillsDirectory",
        "modelsDirectory",
        "triggerMode",
        "wakeWordPhrase",
        "wakeWordSensitivity",
        "wakeCommandSilenceMs",
        "wakeUseVadEndDetection",
        "hudScreenCorner",
        "pttHotkey",
        "selectedLlmPresetIndex",
        "llmPresets",
        "optionalOverlaySkills",
        "selectedFeaturePresetIndex",
        "featurePresets",
    ];

    internal static string SerializeDraft(SettingsDraftDto draft) =>
        JsonSerializer.Serialize(draft, DraftJsonOptions);

    internal static WebUiBridge CreateBridge(
        AppSettings settings,
        ISettingsStore? store = null,
        ISettingsApplyHost? applyHost = null,
        VoiceTriggerMode? runtimeTriggerMode = null,
        IAudioDeviceEnumerator? deviceEnumerator = null)
    {
        var context = new WebUiBridgeContext
        {
            Settings = settings,
            SettingsStore = store ?? new InMemorySettingsStore(settings),
            SettingsApplyHost = applyHost,
            RuntimeTriggerMode = runtimeTriggerMode,
            DeviceEnumerator = deviceEnumerator,
        };
        return new WebUiBridge(context);
    }

    internal static AppSettings CreateRichTemplateSettings()
    {
        var settings = new AppSettings
        {
            MasterEnabled = true,
            PasteToCaretEnabled = false,
            LaunchAtStartup = false,
            PromptRefineEnabled = true,
            ForcedIntent = PromptIntent.GeneralAi,
            OnRefineFailure = OnRefineFailure.ShowError,
            SelectedDeviceId = "device-1",
            CurrentSpeakerUserId = "user-a",
            SpeakerVerifyThreshold = 0.55f,
            SelectedAsrModelId = string.Empty,
            SkillsDirectory = "skills",
            ModelsDirectory = "models",
            TriggerMode = VoiceTriggerMode.Both,
            WakeWordPhrase = "小助手",
            WakeWordSensitivity = WakeWordSensitivity.High,
            WakeCommandSilenceMs = 2200,
            WakeUseVadEndDetection = false,
            HudScreenCorner = HudScreenCorner.TopLeft,
            PttHotkey = "Ctrl+Alt+Space",
            SelectedLlmPresetIndex = 1,
            LlmPresets =
            [
                new() { Name = "Local", ApiBaseUrl = "http://127.0.0.1:11434/v1", ApiKey = "k1", ApiModel = "m1" },
                new() { Name = "Cloud", ApiBaseUrl = "http://127.0.0.1:8080/v1", ApiKey = "k2", ApiModel = "m2" },
                new() { Name = "Backup", ApiBaseUrl = "http://127.0.0.1:9000/v1" },
            ],
            OptionalOverlaySkills = ["voice_refine"],
            PrivacyAcceptedHost = "127.0.0.1",
        };
        settings.MigrateLegacyApiSettings();
        settings.MigrateLegacyFeaturePresets();
        return settings;
    }

    internal sealed class InMemorySettingsStore : ISettingsStore
    {
        public InMemorySettingsStore(AppSettings live) => _live = live;

        private readonly AppSettings _live;

        public AppSettings? LastSaved { get; private set; }

        public AppSettings Load() => _live;

        public void Save(AppSettings settings)
        {
            LastSaved = new AppSettings();
            SettingsCopier.CopyInto(settings, LastSaved);
            SettingsCopier.CopyInto(settings, _live);
        }
    }

    internal sealed class FakeAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        private readonly AudioDeviceInfo[] _devices =
        [
            new() { Id = "mic-default", DisplayName = "Default Mic", IsDefault = true },
            new() { Id = "mic-usb", DisplayName = "USB Mic", IsDefault = false },
        ];

        public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices() => _devices;

        public AudioDeviceInfo? ResolveDevice(string? selectedDeviceId) =>
            _devices.FirstOrDefault(d => d.Id == selectedDeviceId) ?? _devices[0];
    }

    internal sealed class Phase2RecordingApplyHost : ISettingsApplyHost
    {
        private readonly ISettingsStore? _settingsStore;

        public Phase2RecordingApplyHost(AppSettings settings, ISettingsStore? settingsStore = null)
        {
            TargetSettings = settings;
            _settingsStore = settingsStore;
            RegisteredPttHotkey = settings.PttHotkey;
        }

        public AppSettings TargetSettings { get; }

        public int ApplyCount { get; private set; }

        public bool RebuildPipelineCalled { get; private set; }

        public bool TryUpdatePttHotkeyCalled { get; private set; }

        public string? LastHotkeyArg { get; private set; }

        public VoiceTriggerMode LastTriggerMode { get; private set; } = VoiceTriggerMode.PttOnly;

        private readonly RecordingWakeWordDetector _wakeDetector = new();

        public IWakeWordDetector WakeDetector => _wakeDetector;

        public RecordingWakeWordDetector WakeDetectorRecorder => _wakeDetector;

        public string? RegisteredPttHotkey { get; set; } = "Ctrl+Alt+Space";

        public VoiceTriggerMode CurrentTriggerMode => LastTriggerMode;

        public IPushToTalkSource PushToTalk { get; } = new StubPtt();

        public void RebuildPipeline()
        {
            ApplyCount++;
            RebuildPipelineCalled = true;
        }

        public void ApplyPipelineSettings() => ApplyCount++;

        public bool TryUpdatePttHotkey(string hotkey, out string? error)
        {
            TryUpdatePttHotkeyCalled = true;
            LastHotkeyArg = hotkey;
            error = null;
            RegisteredPttHotkey = hotkey;
            return true;
        }

        public void NotifyPttHotkeyUpdated(string hotkeyDisplay)
        {
        }

        public void NotifyPttHotkeyFailed(string? error)
        {
        }

        public void ApplyWakeCaptureSettings(AppSettings settings)
        {
        }

        public void SetVoiceTriggerMode(VoiceTriggerMode mode) => LastTriggerMode = mode;

        public void ApplyHudCorner(HudScreenCorner corner)
        {
        }

        public void ApplyLaunchAtStartup(bool enabled)
        {
        }

        public void InvalidateCaptureDevice()
        {
        }

        public void PersistAndRefresh()
        {
            ApplyCount++;
            _settingsStore?.Save(TargetSettings);
        }

        public void RefreshAudioCaptureAfterSettings()
        {
        }

        public void ShowWakePhraseWarning(string message)
        {
        }
    }

    internal sealed class RecordingWakeWordDetector : IWakeWordDetector
    {
        public string LastPhrase { get; private set; } = string.Empty;

        public WakeWordSensitivity LastSensitivity { get; private set; }

        public string DetectorId => "recording";

        public bool IsRunning => false;

#pragma warning disable CS0067
        public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;
#pragma warning restore CS0067

        public void ApplyPhrase(string phrase) => LastPhrase = phrase;

        public void ApplyWakeSensitivity(WakeWordSensitivity sensitivity) => LastSensitivity = sensitivity;

        public void Dispose()
        {
        }

        public void ProcessAudio(ReadOnlySpan<short> pcm16Mono, int sampleRate)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class StubPtt : IPushToTalkSource
    {
        public string HotkeyDisplay { get; init; } = "Ctrl+Alt+Space";

#pragma warning disable CS0067
        public event EventHandler? PttPressed;

        public event EventHandler? PttReleased;
#pragma warning restore CS0067
    }
}
