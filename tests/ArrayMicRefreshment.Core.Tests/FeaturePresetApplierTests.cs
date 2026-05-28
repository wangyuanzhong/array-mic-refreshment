using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Core.Tests;

public sealed class FeaturePresetApplierTests
{
    [Fact]
    public void ApplyFeaturePreset_sets_llm_preset_index_by_name()
    {
        var settings = new AppSettings
        {
            LlmPresets =
            [
                new() { Name = "Local", ApiBaseUrl = "http://127.0.0.1:11434/v1" },
                new() { Name = "Cloud", ApiBaseUrl = "https://api.example.com/v1" },
                new() { Name = "Backup" },
            ],
            FeaturePresets =
            [
                new FeaturePreset { Name = "Code", LlmPresetName = "Cloud" },
            ],
        };

        FeaturePresetApplier.ApplyFeaturePreset(settings, 0);

        Assert.Equal(1, settings.SelectedLlmPresetIndex);
        Assert.Equal("https://api.example.com/v1", settings.ApiBaseUrl);
    }

    [Fact]
    public void ApplyFeaturePreset_copies_intent_overlay_and_failure()
    {
        var settings = new AppSettings
        {
            LlmPresets = [new() { Name = "Local" }],
            FeaturePresets =
            [
                new FeaturePreset
                {
                    Name = "Research",
                    LlmPresetName = "Local",
                    ForcedIntent = PromptIntent.Research,
                    OnRefineFailure = OnRefineFailure.KeepLast,
                    OptionalOverlaySkills = ["voice_refine", "research_depth"],
                },
            ],
        };

        FeaturePresetApplier.ApplyFeaturePreset(settings, 0);

        Assert.Equal(PromptIntent.Research, settings.ForcedIntent);
        Assert.Equal(OnRefineFailure.KeepLast, settings.OnRefineFailure);
        Assert.Equal(["voice_refine", "research_depth"], settings.OptionalOverlaySkills);
    }

    [Fact]
    public void ApplyFeaturePreset_sets_prompt_refine_enabled_true()
    {
        var settings = new AppSettings
        {
            PromptRefineEnabled = false,
            LlmPresets = [new() { Name = "Local" }],
            FeaturePresets = [new FeaturePreset { Name = "Default", LlmPresetName = "Local" }],
        };

        FeaturePresetApplier.ApplyFeaturePreset(settings, 0);

        Assert.True(settings.PromptRefineEnabled);
    }

    [Fact]
    public void ApplyFeaturePreset_clamps_out_of_range_index()
    {
        var settings = new AppSettings
        {
            LlmPresets = [new() { Name = "A" }, new() { Name = "B" }],
            FeaturePresets =
            [
                new FeaturePreset { Name = "First", LlmPresetName = "A" },
                new FeaturePreset { Name = "Second", LlmPresetName = "B" },
            ],
        };

        FeaturePresetApplier.ApplyFeaturePreset(settings, 99);

        Assert.Equal(1, settings.SelectedFeaturePresetIndex);
        Assert.Equal(PromptIntent.PlainText, settings.ForcedIntent);
    }

    [Fact]
    public void ApplyFeaturePreset_keeps_llm_index_when_name_not_found()
    {
        var settings = new AppSettings
        {
            SelectedLlmPresetIndex = 2,
            LlmPresets =
            [
                new() { Name = "A" },
                new() { Name = "B" },
                new() { Name = "C" },
            ],
            FeaturePresets =
            [
                new FeaturePreset { Name = "Missing", LlmPresetName = "DoesNotExist" },
            ],
        };

        FeaturePresetApplier.ApplyFeaturePreset(settings, 0);

        Assert.Equal(2, settings.SelectedLlmPresetIndex);
    }
}
