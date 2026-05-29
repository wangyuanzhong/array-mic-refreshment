using System.Text.Json;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public sealed class FeaturePresetBridgeTests
{
    [Fact]
    public void ListFeaturePresets_returns_migrated_presets_with_selection()
    {
        var settings = new AppSettings
        {
            LlmPresets =
            [
                new() { Name = "Local" },
                new() { Name = "Cloud" },
            ],
            FeaturePresets = [],
            ForcedIntent = PromptIntent.TaskPlan,
            OptionalOverlaySkills = ["voice_refine"],
        };
        settings.MigrateLegacyApiSettings();
        settings.MigrateLegacyFeaturePresets();

        var bridge = Phase2AcceptanceTestSupport.CreateBridge(settings);
        using var doc = JsonDocument.Parse(bridge.ListFeaturePresets());

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        var item = doc.RootElement[0];
        Assert.Equal("默认", item.GetProperty("name").GetString());
        Assert.Equal("Local", item.GetProperty("llmPresetName").GetString());
        Assert.Equal("TaskPlan", item.GetProperty("forcedIntent").GetString());
        Assert.True(item.GetProperty("selected").GetBoolean());
    }

    [Fact]
    public void ApplyFeaturePreset_updates_settings_and_rebuilds_pipeline()
    {
        var settings = new AppSettings
        {
            PromptRefineEnabled = false,
            LlmPresets =
            [
                new() { Name = "Local" },
                new() { Name = "Cloud", ApiBaseUrl = "https://api.example.com/v1" },
            ],
            FeaturePresets =
            [
                new FeaturePreset
                {
                    Name = "Cloud Code",
                    LlmPresetName = "Cloud",
                    ForcedIntent = PromptIntent.CodeEditing,
                    OptionalOverlaySkills = ["code_style"],
                },
            ],
        };
        var host = new Phase2AcceptanceTestSupport.Phase2RecordingApplyHost(settings);
        var bridge = Phase2AcceptanceTestSupport.CreateBridge(settings, applyHost: host);

        using var doc = JsonDocument.Parse(bridge.ApplyFeaturePreset(0));

        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(settings.PromptRefineEnabled);
        Assert.Equal(1, settings.SelectedLlmPresetIndex);
        Assert.Equal(PromptIntent.CodeEditing, settings.ForcedIntent);
        Assert.Equal(["code_style"], settings.OptionalOverlaySkills);
        Assert.True(host.RebuildPipelineCalled);
    }
}
