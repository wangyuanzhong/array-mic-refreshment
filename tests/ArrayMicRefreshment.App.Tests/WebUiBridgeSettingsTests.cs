using System.Net;
using System.Text;
using System.Text.Json;
using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public class WebUiBridgeSettingsTests
{
    [Fact]
    public void GetAppInfo_returns_version_json()
    {
        var bridge = CreateBridge(new AppSettings());
        var json = bridge.GetAppInfo();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(AppInfo.Version, doc.RootElement.GetProperty("version").GetString());
        Assert.Equal("win-x64", doc.RootElement.GetProperty("platform").GetString());
    }

    [Fact]
    public void LoadSettingsDraft_includes_runtime_trigger_mode()
    {
        var settings = new AppSettings { TriggerMode = VoiceTriggerMode.PttOnly };
        var bridge = CreateBridge(settings, runtimeTriggerMode: VoiceTriggerMode.WakeWordOnly);
        using var doc = JsonDocument.Parse(bridge.LoadSettingsDraft());
        Assert.Equal("WakeWordOnly", doc.RootElement.GetProperty("triggerMode").GetString());
    }

    [Fact]
    public void SaveSettingsDraft_without_apply_host_persists_with_warning()
    {
        var settings = new AppSettings { PttHotkey = "Ctrl+Alt+Space" };
        var store = new InMemorySettingsStore(settings);
        var bridge = CreateBridge(settings, store);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.PttHotkey = "Ctrl+Shift+A";
        var draftJson = JsonSerializer.Serialize(draft);

        using var doc = JsonDocument.Parse(bridge.SaveSettingsDraft(draftJson));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("SettingsApplyHost", doc.RootElement.GetProperty("warning").GetString());
        Assert.Equal("Ctrl+Shift+A", settings.PttHotkey);
        Assert.Equal("Ctrl+Shift+A", store.LastSaved?.PttHotkey);
    }

    [Fact]
    public void SaveSettingsDraft_with_apply_host_invokes_service()
    {
        var settings = new AppSettings { PttHotkey = "Ctrl+Alt+Space" };
        var store = new InMemorySettingsStore(settings);
        var host = new RecordingApplyHost(settings);
        var bridge = CreateBridge(settings, store, host);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.PttHotkey = "Ctrl+Shift+B";
        var draftJson = JsonSerializer.Serialize(draft);

        using var doc = JsonDocument.Parse(bridge.SaveSettingsDraft(draftJson));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(doc.RootElement.TryGetProperty("warning", out _));
        Assert.True(host.ApplyCount >= 1);
        Assert.Equal("Ctrl+Shift+B", settings.PttHotkey);
    }

    [Fact]
    public void GetPrivacyConsentState_flags_remote_host()
    {
        var settings = new AppSettings();
        var bridge = CreateBridge(settings);
        using var doc = JsonDocument.Parse(bridge.GetPrivacyConsentState("https://api.openai.com/v1"));
        Assert.True(doc.RootElement.GetProperty("needsPrompt").GetBoolean());
        Assert.Equal("api.openai.com", doc.RootElement.GetProperty("host").GetString());
    }

    private static WebUiBridge CreateBridge(
        AppSettings settings,
        ISettingsStore? store = null,
        ISettingsApplyHost? applyHost = null,
        VoiceTriggerMode? runtimeTriggerMode = null)
    {
        var context = new WebUiBridgeContext
        {
            Settings = settings,
            SettingsStore = store ?? new InMemorySettingsStore(settings),
            SettingsApplyHost = applyHost,
            RuntimeTriggerMode = runtimeTriggerMode,
        };
        return new WebUiBridge(context);
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly AppSettings _live;

        public InMemorySettingsStore(AppSettings live) => _live = live;

        public AppSettings? LastSaved { get; private set; }

        public AppSettings Load() => _live;

        public void Save(AppSettings settings)
        {
            LastSaved = new AppSettings();
            SettingsCopier.CopyInto(settings, LastSaved);
            SettingsCopier.CopyInto(settings, _live);
        }
    }

    private sealed class RecordingApplyHost : ISettingsApplyHost
    {
        public RecordingApplyHost(AppSettings settings) => TargetSettings = settings;

        public int ApplyCount { get; private set; }

        public AppSettings TargetSettings { get; }

        public VoiceTriggerMode CurrentTriggerMode => VoiceTriggerMode.PttOnly;

        public string? RegisteredPttHotkey { get; set; }

        public void RebuildPipeline() => ApplyCount++;

        public void ApplyPipelineSettings() => ApplyCount++;

        public IPushToTalkSource PushToTalk => new NoOpPtt();

        public bool TryUpdatePttHotkey(string hotkey, out string? error)
        {
            error = null;
            return true;
        }

        public void NotifyPttHotkeyUpdated(string hotkeyDisplay)
        {
        }

        public void NotifyPttHotkeyFailed(string? error)
        {
        }

        public IWakeWordDetector WakeDetector => new NoOpWakeDetector();

        public void ApplyWakeCaptureSettings(AppSettings settings)
        {
        }

        public void SetVoiceTriggerMode(VoiceTriggerMode mode)
        {
        }

        public void ApplyHudCorner(HudScreenCorner corner)
        {
        }

        public void ApplyLaunchAtStartup(bool enabled)
        {
        }

        public void InvalidateCaptureDevice()
        {
        }

        public void PersistAndRefresh() => ApplyCount++;

        public void RefreshAudioCaptureAfterSettings()
        {
        }

        public void ShowWakePhraseWarning(string message)
        {
        }
    }

    private sealed class NoOpPtt : IPushToTalkSource
    {
        public string HotkeyDisplay => "Ctrl+Space";

#pragma warning disable CS0067
        public event EventHandler? PttPressed;

        public event EventHandler? PttReleased;
#pragma warning restore CS0067
    }

    private sealed class NoOpWakeDetector : IWakeWordDetector
    {
        public string DetectorId => "noop";

        public bool IsRunning => false;

#pragma warning disable CS0067
        public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;
#pragma warning restore CS0067

        public void ApplyPhrase(string phrase)
        {
        }

        public void ApplyWakeSensitivity(WakeWordSensitivity sensitivity)
        {
        }

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
}
