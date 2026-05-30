using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public class SettingsDraftValidatorTests
{
    [Fact]
    public void Validate_rejects_invalid_hotkey()
    {
        var template = new AppSettings();
        var draft = SettingsDraftMapper.ToDraft(template, null);
        draft.PttHotkey = "NotAValidHotkey!!!";

        var result = SettingsDraftValidator.Validate(draft, template);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "pttHotkey");
    }

    [Fact]
    public void Validate_requires_wake_phrase_for_wake_only_mode()
    {
        var template = new AppSettings();
        var draft = SettingsDraftMapper.ToDraft(template, null);
        draft.TriggerMode = VoiceTriggerMode.WakeWordOnly;
        draft.WakeWordPhrase = "   ";

        var result = SettingsDraftValidator.Validate(draft, template);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "wakeWordPhrase");
    }

    [Fact]
    public void Validate_rejects_wake_phrase_that_cannot_be_encoded_when_kws_installed()
    {
        if (!WakeWordModelPaths.TryResolve("models", out _))
        {
            return;
        }

        var template = new AppSettings { ModelsDirectory = "models" };
        var draft = SettingsDraftMapper.ToDraft(template, null);
        draft.WakeWordPhrase = "自定义测试词";

        var result = SettingsDraftValidator.Validate(draft, template);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "wakeWordPhrase");
    }

    [Fact]
    public void Validate_requires_api_url_when_refine_enabled()
    {
        var template = new AppSettings();
        var draft = SettingsDraftMapper.ToDraft(template, null);
        draft.PromptRefineEnabled = true;
        draft.LlmPresets[0].ApiBaseUrl = string.Empty;

        var result = SettingsDraftValidator.Validate(draft, template);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Field == "apiBaseUrl");
    }
}
