using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Core.Tests;

public sealed class FeaturePresetMigrationTests
{
    [Fact]
    public void MigrateLegacyFeaturePresets_creates_default_from_legacy_fields()
    {
        var settings = new AppSettings
        {
            PromptRefineEnabled = true,
            ForcedIntent = PromptIntent.CodeEditing,
            OnRefineFailure = OnRefineFailure.ShowError,
            OptionalOverlaySkills = ["voice_refine"],
            LlmPresets =
            [
                new() { Name = "My Local" },
                new() { Name = "Cloud" },
                new() { Name = "Backup" },
            ],
            SelectedLlmPresetIndex = 0,
            FeaturePresets = [],
        };

        settings.MigrateLegacyApiSettings();
        settings.MigrateLegacyFeaturePresets();

        Assert.Single(settings.FeaturePresets);
        var preset = settings.FeaturePresets[0];
        Assert.Equal("默认", preset.Name);
        Assert.Equal("My Local", preset.LlmPresetName);
        Assert.Equal(PromptIntent.CodeEditing, preset.ForcedIntent);
        Assert.Equal(OnRefineFailure.ShowError, preset.OnRefineFailure);
        Assert.Equal(["voice_refine"], preset.OptionalOverlaySkills);
        Assert.Equal(0, settings.SelectedFeaturePresetIndex);
    }

    [Fact]
    public void MigrateLegacyFeaturePresets_does_not_overwrite_existing_presets()
    {
        var settings = new AppSettings
        {
            ForcedIntent = PromptIntent.GeneralAi,
            FeaturePresets =
            [
                new FeaturePreset
                {
                    Name = "Custom",
                    LlmPresetName = "Cloud",
                    ForcedIntent = PromptIntent.Research,
                },
            ],
            SelectedFeaturePresetIndex = 0,
        };

        settings.MigrateLegacyFeaturePresets();

        Assert.Single(settings.FeaturePresets);
        Assert.Equal("Custom", settings.FeaturePresets[0].Name);
        Assert.Equal(PromptIntent.Research, settings.FeaturePresets[0].ForcedIntent);
    }

    [Fact]
    public void Load_old_json_without_feature_presets_migrates_on_load()
    {
        var path = Path.Combine(Path.GetTempPath(), $"amr-fp-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "promptRefineEnabled": true,
                  "forcedIntent": 2,
                  "onRefineFailure": 2,
                  "optionalOverlaySkills": ["dictation_cleanup"],
                  "selectedLlmPresetIndex": 1,
                  "llmPresets": [
                    { "name": "Local", "apiBaseUrl": "http://127.0.0.1:11434/v1", "apiKey": "", "apiModel": "m1" },
                    { "name": "Cloud", "apiBaseUrl": "https://api.example.com/v1", "apiKey": "k", "apiModel": "m2" },
                    { "name": "Backup", "apiBaseUrl": "https://api.openai.com/v1", "apiKey": "", "apiModel": "gpt-4o-mini" }
                  ]
                }
                """);

            var loaded = new JsonSettingsStore(path).Load();

            Assert.Single(loaded.FeaturePresets);
            Assert.Equal("Cloud", loaded.FeaturePresets[0].LlmPresetName);
            Assert.Equal(PromptIntent.GeneralAi, loaded.FeaturePresets[0].ForcedIntent);
            Assert.Equal(OnRefineFailure.KeepLast, loaded.FeaturePresets[0].OnRefineFailure);
            Assert.Equal(["dictation_cleanup"], loaded.FeaturePresets[0].OptionalOverlaySkills);
            Assert.Equal(PromptIntent.GeneralAi, loaded.ForcedIntent);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void MigrateLegacyFeaturePresets_clamps_selected_index()
    {
        var settings = new AppSettings
        {
            FeaturePresets =
            [
                new FeaturePreset { Name = "Only" },
            ],
            SelectedFeaturePresetIndex = 5,
        };

        settings.MigrateLegacyFeaturePresets();

        Assert.Equal(0, settings.SelectedFeaturePresetIndex);
    }
}
