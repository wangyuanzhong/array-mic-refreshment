#if WINDOWS

using System.Text.Json;
using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Integration.Tests.Support;

/// <summary>
/// Tray-like stack: Web settings save → <see cref="SettingsApplyService"/> → real PTT hook + capture.
/// </summary>
internal sealed class PttTrayLikeE2eHarness : ISettingsApplyHost, IDisposable
{
    private static readonly JsonSerializerOptions DraftJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly SettingsApplyService _applyService = new();
    private readonly Form _anchor;
    private readonly LowLevelHotkeyHost _hotkeyHost;
    private readonly NAudioPushToTalkSource _ptt;
    private readonly PttCaptureService _capture;
    private readonly InMemorySettingsStore _store;

    public PttTrayLikeE2eHarness(AppSettings settings)
    {
        Settings = settings;
        _store = new InMemorySettingsStore(settings);
        RegisteredPttHotkey = null;

        _anchor = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
        };

        _hotkeyHost = new LowLevelHotkeyHost();
        _hotkeyHost.BindUiAnchor(_anchor);
        _ptt = new NAudioPushToTalkSource(settings.PttHotkey, _hotkeyHost);
        var device = new AudioDeviceInfo
        {
            Id = "fake:e2e",
            DisplayName = "E2E Fake Mic",
            HostApi = AudioHostApi.Wasapi,
            IsDefault = true,
            DefaultSampleRate = 16000,
            Channels = 1,
        };
        var payload = PcmResampler.GenerateSineWavePcm16(16000, 1, 440, TimeSpan.FromMilliseconds(500));
        var enumerator = new FakeAudioDeviceEnumerator(device);
        var factory = new FakeCaptureStreamFactory(_ => new FakeCaptureStream(payload, 16000, 1));
        var vad = new QuickEndVad();

        _capture = new PttCaptureService(
            settings,
            _ptt,
            enumerator,
            factory,
            vad,
            pttCaptureAllowed: () => true,
            keepStandbyCaptureBetweenSessions: () => false,
            vadAssistMinHold: TimeSpan.Zero);

        var context = new WebUiBridgeContext
        {
            Settings = settings,
            SettingsStore = _store,
            SettingsApplyHost = this,
            SettingsApplyService = _applyService,
            RuntimeTriggerMode = VoiceTriggerMode.PttOnly,
            MasterEnabled = true,
            DeviceEnumerator = enumerator,
        };
        Bridge = new WebUiBridge(context);
        WakeDetector = new StubWakeWordDetector(settings.WakeWordPhrase);
    }

    public AppSettings Settings { get; }

    public WebUiBridge Bridge { get; }

    public Form AnchorForm => _anchor;

    public bool IsRecording => _capture.IsPttHeld;

    public PttCaptureService CaptureService => _capture;

    public VoiceTriggerMode CurrentTriggerMode { get; private set; } = VoiceTriggerMode.PttOnly;

    public string? RegisteredPttHotkey { get; set; }

    AppSettings ISettingsApplyHost.TargetSettings => Settings;

    public IWakeWordDetector WakeDetector { get; }

    public IPushToTalkSource PushToTalk => _ptt;

    public void Show()
    {
        _anchor.Show();
    }

    /// <summary>Same path as Web 设置页保存：改 PTT 热键并应用到运行时。</summary>
    public void SaveHotkeyViaSettingsUi(string newHotkey)
    {
        var draft = SettingsDraftMapper.ToDraft(Settings, CurrentTriggerMode);
        draft.PttHotkey = newHotkey;
        draft.TriggerMode = VoiceTriggerMode.PttOnly;
        draft.MasterEnabled = true;
        draft.PromptRefineEnabled = false;
        draft.FeaturePresets = [];
        draft.LlmPresets =
        [
            new LlmPresetDto
            {
                Name = "Local",
                ApiBaseUrl = "http://127.0.0.1:11434/v1",
                ApiModel = "test",
            },
        ];
        draft.SelectedLlmPresetIndex = 0;

        var json = JsonSerializer.Serialize(draft, DraftJsonOptions);
        using var doc = JsonDocument.Parse(Bridge.SaveSettingsDraft(json));
        var root = doc.RootElement;
        if (!root.GetProperty("ok").GetBoolean())
        {
            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
            throw new InvalidOperationException($"SaveSettingsDraft failed: {err}");
        }
    }

    public void EnsurePttHotkeyRegistered()
    {
        if (_ptt is NAudioPushToTalkSource naudio && !naudio.IsRegistered)
        {
            if (!naudio.TryUpdateHotkey(Settings.PttHotkey, out var error))
            {
                throw new InvalidOperationException($"PTT hook register failed: {error}");
            }

            RegisteredPttHotkey = Settings.PttHotkey;
        }
    }

    public void RebuildPipeline()
    {
    }

    public void ApplyPipelineSettings()
    {
    }

    public bool TryUpdatePttHotkey(string hotkey, out string? error)
    {
        if (_ptt is NAudioPushToTalkSource naudio)
        {
            return naudio.TryUpdateHotkey(hotkey, out error);
        }

        error = "PTT source unavailable";
        return false;
    }

    public void NotifyPttHotkeyUpdated(string hotkeyDisplay)
    {
    }

    public void NotifyPttHotkeyFailed(string? error)
    {
        throw new InvalidOperationException(error ?? "PTT hotkey update failed");
    }

    public void ApplyWakeCaptureSettings(AppSettings settings)
    {
    }

    public void SetVoiceTriggerMode(VoiceTriggerMode mode) => CurrentTriggerMode = mode;

    public void ApplyHudCorner(HudScreenCorner corner)
    {
    }

    public void ApplyLaunchAtStartup(bool enabled)
    {
    }

    public void InvalidateCaptureDevice() => _capture.InvalidateCaptureDevice();

    public void PersistAndRefresh()
    {
        _store.Save(Settings);
    }

    public void RefreshAudioCaptureAfterSettings() => _capture.StopStandbyListening();

    public void ShowWakePhraseWarning(string message)
    {
    }

    public void Dispose()
    {
        _capture.Dispose();
        _ptt.Dispose();
        _hotkeyHost.Dispose();
        if (_anchor.IsHandleCreated && !_anchor.IsDisposed)
        {
            _anchor.Close();
            _anchor.Dispose();
        }
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        public InMemorySettingsStore(AppSettings live) => _live = live;

        private readonly AppSettings _live;

        public AppSettings Load() => _live;

        public void Save(AppSettings settings)
        {
            _live.PttHotkey = settings.PttHotkey;
            _live.TriggerMode = settings.TriggerMode;
            _live.MasterEnabled = settings.MasterEnabled;
            _live.PromptRefineEnabled = settings.PromptRefineEnabled;
        }
    }

    private sealed class QuickEndVad : IVoiceActivityDetector
    {
        private int _frames;

        public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate)
        {
            _frames++;
            return _frames >= 2;
        }

        public void Reset() => _frames = 0;
    }
}

#endif
