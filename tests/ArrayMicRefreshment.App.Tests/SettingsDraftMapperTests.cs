using System.Text.Json;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public class SettingsDraftMapperTests
{
    [Fact]
    public void ToDraft_uses_runtime_trigger_mode_when_provided()
    {
        var settings = new AppSettings { TriggerMode = VoiceTriggerMode.PttOnly };
        var draft = SettingsDraftMapper.ToDraft(settings, VoiceTriggerMode.Both);
        Assert.Equal(VoiceTriggerMode.Both, draft.TriggerMode);
    }

    [Fact]
    public void RoundTrip_preserves_llm_presets_and_overlay_skills()
    {
        var settings = new AppSettings
        {
            LlmPresets =
            [
                new() { Name = "Local", ApiBaseUrl = "http://127.0.0.1:11434/v1", ApiModel = "llama" },
                new() { Name = "Cloud" },
                new() { Name = "Backup" },
            ],
            OptionalOverlaySkills = ["code_style"],
            SelectedLlmPresetIndex = 0,
        };
        settings.MigrateLegacyApiSettings();

        var draft = SettingsDraftMapper.ToDraft(settings, null);
        draft.PttHotkey = "Ctrl+Shift+Space";
        draft.OptionalOverlaySkills.Add("research_depth");

        var mapped = SettingsDraftMapper.ToAppSettings(draft, settings);
        Assert.Equal("Ctrl+Shift+Space", mapped.PttHotkey);
        Assert.Equal(2, mapped.OptionalOverlaySkills.Count);
        Assert.Equal("http://127.0.0.1:11434/v1", mapped.CurrentPreset.ApiBaseUrl);
    }
}
