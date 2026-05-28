using System.Net.Http;
using System.Text;
using System.Text.Json;
using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;
using static ArrayMicRefreshment.App.Tests.Phase2AcceptanceTestSupport;

namespace ArrayMicRefreshment.App.Tests;

/// <summary>
/// Automated coverage for Route B Phase 2 (Web settings parity).
/// Maps to docs/UI_ROUTE_B_WEBVIEW2.md §8 checklist — run on Windows via scripts/test-phase2-route-b.ps1.
/// Does NOT replace §10.2 manual PTT/wake/mic regression.
/// </summary>
[Trait("Phase", "RouteB2")]
public sealed class Phase2AcceptanceTests
{
    [Fact]
    public void Phase2_A01_LoadDraft_exposes_all_section_json_properties()
    {
        var settings = CreateRichTemplateSettings();
        var bridge = CreateBridge(settings, runtimeTriggerMode: VoiceTriggerMode.WakeWordOnly);

        using var doc = JsonDocument.Parse(bridge.LoadSettingsDraft());
        foreach (var name in DraftJsonPropertyNames)
        {
            Assert.True(
                doc.RootElement.TryGetProperty(name, out _),
                $"LoadSettingsDraft missing property '{name}' (§7.3).");
        }

        Assert.Equal("WakeWordOnly", doc.RootElement.GetProperty("triggerMode").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("llmPresets").GetArrayLength());
    }

    [Fact]
    public void Phase2_A02_RoundTrip_preserves_device_asr_speaker_and_wake_fields()
    {
        var template = CreateRichTemplateSettings();
        var draft = SettingsDraftMapper.ToDraft(template, VoiceTriggerMode.Both);
        draft.SelectedDeviceId = "mic-usb";
        draft.CurrentSpeakerUserId = "user-b";
        draft.SpeakerVerifyThreshold = 0.62f;
        draft.SelectedAsrModelId = "paraformer";
        draft.WakeWordPhrase = "你好助手";
        draft.WakeCommandSilenceMs = 1800;

        var mapped = SettingsDraftMapper.ToAppSettings(draft, template);

        Assert.Equal("mic-usb", mapped.SelectedDeviceId);
        Assert.Equal("user-b", mapped.CurrentSpeakerUserId);
        Assert.Equal(0.62f, mapped.SpeakerVerifyThreshold, 2);
        Assert.Equal("paraformer", mapped.SelectedAsrModelId);
        Assert.Equal("你好助手", mapped.WakeWordPhrase);
        Assert.Equal(1800, mapped.WakeCommandSilenceMs);
    }

    [Fact]
    public void Phase2_A03_Llm_presets_save_via_bridge_updates_active_preset()
    {
        var settings = CreateRichTemplateSettings();
        var store = new InMemorySettingsStore(settings);
        var host = new Phase2RecordingApplyHost(settings, store);
        var bridge = CreateBridge(settings, store, host);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.SelectedLlmPresetIndex = 0;
        draft.LlmPresets[0].ApiBaseUrl = "http://127.0.0.1:8080/v1";
        draft.LlmPresets[0].ApiModel = "qwen-test";
        draft.LlmPresets[1].Name = "Renamed Cloud";

        using var saveDoc = JsonDocument.Parse(bridge.SaveSettingsDraft(SerializeDraft(draft)));
        Assert.True(saveDoc.RootElement.GetProperty("ok").GetBoolean());

        Assert.Equal("http://127.0.0.1:8080/v1", settings.ApiBaseUrl);
        Assert.Equal("qwen-test", settings.ApiModel);
        Assert.Equal("Renamed Cloud", settings.LlmPresets[1].Name);
        Assert.True(host.ApplyCount >= 1);
    }

    [Fact]
    public void Phase2_A04_Refine_and_overlay_skills_toggle_rebuilds_pipeline()
    {
        var settings = CreateRichTemplateSettings();
        settings.PromptRefineEnabled = false;
        var host = new Phase2RecordingApplyHost(settings);
        var bridge = CreateBridge(settings, applyHost: host);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.PromptRefineEnabled = true;
        draft.ForcedIntent = PromptIntent.CodeEditing;
        draft.OnRefineFailure = OnRefineFailure.KeepLast;
        draft.OptionalOverlaySkills = ["voice_refine", "dictation_cleanup"];

        using var doc = JsonDocument.Parse(bridge.SaveSettingsDraft(SerializeDraft(draft)));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(host.RebuildPipelineCalled);
        Assert.Equal(PromptIntent.CodeEditing, settings.ForcedIntent);
        Assert.Equal(2, settings.OptionalOverlaySkills.Count);
    }

    [Fact]
    public void Phase2_A05_Trigger_mode_wake_fields_and_hotkey_apply_via_host()
    {
        var settings = CreateRichTemplateSettings();
        settings.TriggerMode = VoiceTriggerMode.PttOnly;
        var host = new Phase2RecordingApplyHost(settings);
        var bridge = CreateBridge(settings, applyHost: host);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.TriggerMode = VoiceTriggerMode.Both;
        draft.WakeWordPhrase = "测试唤醒";
        draft.WakeWordSensitivity = WakeWordSensitivity.Maximum;
        draft.PttHotkey = "Ctrl+Shift+Space";
        draft.HudScreenCorner = HudScreenCorner.BottomLeft;
        draft.LaunchAtStartup = true;

        using var doc = JsonDocument.Parse(bridge.SaveSettingsDraft(SerializeDraft(draft)));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());

        Assert.True(host.TryUpdatePttHotkeyCalled);
        Assert.Equal("Ctrl+Shift+Space", host.LastHotkeyArg);
        Assert.Equal("测试唤醒", host.WakeDetectorRecorder.LastPhrase);
        Assert.Equal(WakeWordSensitivity.Maximum, host.WakeDetectorRecorder.LastSensitivity);
        Assert.Equal(VoiceTriggerMode.Both, host.LastTriggerMode);
        Assert.Equal(HudScreenCorner.BottomLeft, settings.HudScreenCorner);
        Assert.True(settings.LaunchAtStartup);
    }

    [Fact]
    public void Phase2_A06_ValidateSettingsDraft_rejects_invalid_hotkey_and_wake_phrase()
    {
        var settings = CreateRichTemplateSettings();
        var bridge = CreateBridge(settings);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.PttHotkey = "!!!invalid!!!";
        draft.TriggerMode = VoiceTriggerMode.WakeWordOnly;
        draft.WakeWordPhrase = "  ";

        using var doc = JsonDocument.Parse(bridge.ValidateSettingsDraft(SerializeDraft(draft)));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        var fields = doc.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("pttHotkey", fields);
        Assert.Contains("wakeWordPhrase", fields);
    }

    [Fact]
    public void Phase2_A07_ListAudioDevices_and_AsrModels_return_entries()
    {
        var settings = CreateRichTemplateSettings();
        var bridge = CreateBridge(settings, deviceEnumerator: new FakeAudioDeviceEnumerator());

        using var devicesDoc = JsonDocument.Parse(bridge.ListAudioDevices());
        Assert.Equal(2, devicesDoc.RootElement.GetArrayLength());

        using var modelsDoc = JsonDocument.Parse(bridge.ListAsrModels());
        Assert.True(modelsDoc.RootElement.GetArrayLength() >= 1);
    }

    [Fact]
    public void Phase2_A08_Skills_catalog_and_overlay_list_when_repo_skills_present()
    {
        var settings = new AppSettings { SkillsDirectory = "skills" };
        var bridge = CreateBridge(settings);

        using var statusDoc = JsonDocument.Parse(bridge.GetSkillsCatalogStatus());
        var missing = statusDoc.RootElement.GetProperty("missingFiles");
        Assert.Equal(JsonValueKind.Array, missing.ValueKind);

        using var overlayDoc = JsonDocument.Parse(bridge.ListOptionalOverlaySkills());
        Assert.True(overlayDoc.RootElement.GetArrayLength() >= 1);
    }

    [Fact]
    public void Phase2_A09_Save_persists_master_toggle_and_api_key_in_store()
    {
        var settings = CreateRichTemplateSettings();
        settings.LlmPresets[0].ApiKey = "secret-local-key";
        var store = new InMemorySettingsStore(settings);
        var host = new Phase2RecordingApplyHost(settings, store);
        var bridge = CreateBridge(settings, store, host);

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.MasterEnabled = false;

        using var doc = JsonDocument.Parse(bridge.SaveSettingsDraft(SerializeDraft(draft)));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());

        Assert.NotNull(store.LastSaved);
        Assert.Equal("secret-local-key", store.LastSaved!.LlmPresets[0].ApiKey);
        Assert.False(settings.MasterEnabled);
    }

    [Fact]
    public void Phase2_A10_TestLlmConnection_with_stub_http_succeeds_for_valid_intent()
    {
        var call = 0;
        var handler = new StubHttpHandler(_ =>
        {
            call++;
            var assistant = call == 1
                ? """{"intent":"general_chat","confidence":0.91}"""
                : "refined";
            var body =
                "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":" +
                JsonSerializer.Serialize(assistant) +
                "}}]}";
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        var settings = CreateRichTemplateSettings();
        settings.PromptRefineEnabled = true;
        settings.ForcedIntent = PromptIntent.Auto;
        settings.ApiBaseUrl = "https://api.example.com/v1";
        settings.ApiKey = "key";
        settings.ApiModel = "model";

        var result = LlmConnectionTester.TestAsync(settings, handler).GetAwaiter().GetResult();
        Assert.True(result.Ok);
        Assert.True(result.RouterConfidence > 0.8f);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_factory(request));
    }
}
