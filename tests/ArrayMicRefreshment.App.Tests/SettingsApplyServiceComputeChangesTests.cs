using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public sealed class SettingsApplyServiceComputeChangesTests
{
    [Fact]
    public void ComputeChanges_flags_pipeline_rebuild_when_prompt_refine_toggles()
    {
        var previous = new AppSettings { PromptRefineEnabled = false };
        var current = new AppSettings { PromptRefineEnabled = true };

        var result = SettingsApplyService.ComputeChanges(
            previous,
            current,
            VoiceTriggerMode.PttOnly,
            "Ctrl+Alt+Space");

        Assert.True(result.PipelineRebuildRequired);
        Assert.False(result.HotkeyChanged);
    }

    [Fact]
    public void ComputeChanges_flags_hotkey_change_against_registered_hotkey()
    {
        var previous = new AppSettings { PttHotkey = "Ctrl+Alt+Space" };
        var current = new AppSettings { PttHotkey = "Ctrl+Shift+Space" };

        var result = SettingsApplyService.ComputeChanges(
            previous,
            current,
            VoiceTriggerMode.PttOnly,
            registeredPttHotkey: "Ctrl+Alt+Space");

        Assert.True(result.HotkeyChanged);
        Assert.False(result.PipelineRebuildRequired);
    }

    [Fact]
    public void ComputeChanges_ignores_hotkey_when_registered_matches_new_value()
    {
        var previous = new AppSettings { PttHotkey = "Ctrl+Alt+Space" };
        var current = new AppSettings { PttHotkey = "Ctrl+Shift+Space" };

        var result = SettingsApplyService.ComputeChanges(
            previous,
            current,
            VoiceTriggerMode.PttOnly,
            registeredPttHotkey: "Ctrl+Shift+Space");

        Assert.False(result.HotkeyChanged);
    }

    [Fact]
    public void ComputeChanges_flags_trigger_mode_when_runtime_differs_from_persisted()
    {
        var previous = new AppSettings { TriggerMode = VoiceTriggerMode.PttOnly };
        var current = new AppSettings { TriggerMode = VoiceTriggerMode.Both };

        var result = SettingsApplyService.ComputeChanges(
            previous,
            current,
            currentTriggerMode: VoiceTriggerMode.PttOnly,
            registeredPttHotkey: "Ctrl+Alt+Space");

        Assert.True(result.TriggerModeChanged);
        Assert.True(result.WakeCaptureRestartRequired);
    }

    [Fact]
    public void ComputeChanges_flags_wake_phrase_and_sensitivity_independently()
    {
        var previous = new AppSettings
        {
            WakeWordPhrase = "小助手",
            WakeWordSensitivity = WakeWordSensitivity.Standard,
            WakeCommandSilenceMs = 3000,
        };
        var current = new AppSettings
        {
            WakeWordPhrase = "你好",
            WakeWordSensitivity = WakeWordSensitivity.High,
            WakeCommandSilenceMs = 2500,
        };

        var result = SettingsApplyService.ComputeChanges(
            previous,
            current,
            VoiceTriggerMode.Both,
            "Ctrl+Alt+Space");

        Assert.True(result.WakePhraseChanged);
        Assert.True(result.WakeSensitivityChanged);
        Assert.True(result.WakeCommandSilenceChanged);
    }

    [Fact]
    public void ComputeChanges_flags_hud_and_startup_changes()
    {
        var previous = new AppSettings
        {
            HudScreenCorner = HudScreenCorner.BottomRight,
            LaunchAtStartup = true,
        };
        var current = new AppSettings
        {
            HudScreenCorner = HudScreenCorner.TopLeft,
            LaunchAtStartup = false,
        };

        var result = SettingsApplyService.ComputeChanges(
            previous,
            current,
            VoiceTriggerMode.PttOnly,
            "Ctrl+Alt+Space");

        Assert.True(result.HudCornerChanged);
        Assert.True(result.LaunchAtStartupChanged);
    }

    [Theory]
    [InlineData(VoiceTriggerMode.PttOnly)]
    [InlineData(VoiceTriggerMode.WakeWordOnly)]
    [InlineData(VoiceTriggerMode.Both)]
    public void NormalizePersistedMode_preserves_known_modes(VoiceTriggerMode mode)
    {
        Assert.Equal(mode, SettingsApplyService.NormalizePersistedMode(mode));
    }

    [Fact]
    public void CloneSnapshot_copies_all_persisted_fields()
    {
        var source = new AppSettings
        {
            MasterEnabled = false,
            PttHotkey = "F8",
            WakeWordPhrase = "测试",
            SelectedLlmPresetIndex = 1,
            LlmPresets =
            {
                new LlmPreset { Name = "A", ApiKey = "secret" },
                new LlmPreset { Name = "B" },
            },
            OptionalOverlaySkills = { "skill-a" },
        };

        var clone = SettingsApplyService.CloneSnapshot(source);

        Assert.NotSame(source, clone);
        Assert.Equal(source.MasterEnabled, clone.MasterEnabled);
        Assert.Equal(source.PttHotkey, clone.PttHotkey);
        Assert.Equal(source.WakeWordPhrase, clone.WakeWordPhrase);
        Assert.Equal(source.SelectedLlmPresetIndex, clone.SelectedLlmPresetIndex);
        Assert.Equal("secret", clone.LlmPresets[0].ApiKey);
        Assert.Equal(source.OptionalOverlaySkills, clone.OptionalOverlaySkills);
    }
}
