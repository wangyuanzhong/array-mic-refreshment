using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Core.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_round_trips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"amr-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(path);
            var original = new AppSettings
            {
                MasterEnabled = false,
                PasteToCaretEnabled = true,
                PromptRefineEnabled = true,
                ApiBaseUrl = "http://127.0.0.1:11434/v1",
                ApiModel = "qwen-test",
                PttHotkey = "Ctrl+Alt+V",
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.False(loaded.MasterEnabled);
            Assert.True(loaded.PasteToCaretEnabled);
            Assert.True(loaded.PromptRefineEnabled);
            Assert.Equal("http://127.0.0.1:11434/v1", loaded.ApiBaseUrl);
            Assert.Equal("qwen-test", loaded.ApiModel);
            Assert.Equal("Ctrl+Alt+V", loaded.PttHotkey);
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
    public void SaveAndLoad_round_trips_trigger_mode_and_wake_word_phrase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"amr-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(path);
            var original = new AppSettings
            {
                TriggerMode = VoiceTriggerMode.WakeWordOnly,
                WakeWordPhrase = "开始听写",
                PttHotkey = "Ctrl+Alt+Space",
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(VoiceTriggerMode.WakeWordOnly, loaded.TriggerMode);
            Assert.Equal("开始听写", loaded.WakeWordPhrase);
            Assert.Equal("Ctrl+Alt+Space", loaded.PttHotkey);
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
    public void Load_migrates_legacy_pttRecordingMode_toggle_to_manual_trigger()
    {
        var path = Path.Combine(Path.GetTempPath(), $"amr-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "triggerMode": "PttOnly",
                  "pttRecordingMode": "Toggle",
                  "pttHotkey": "Ctrl+Alt+Space"
                }
                """);

            var loaded = new JsonSettingsStore(path).Load();

            Assert.Equal(VoiceTriggerMode.Manual, loaded.TriggerMode);
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
    public void Load_without_trigger_fields_defaults_to_ptt_and_default_wake_phrase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"amr-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "masterEnabled": true,
                  "pttHotkey": "Ctrl+Shift+Space"
                }
                """);

            var loaded = new JsonSettingsStore(path).Load();

            Assert.Equal(VoiceTriggerMode.PttOnly, loaded.TriggerMode);
            Assert.Equal("小助手", loaded.WakeWordPhrase);
            Assert.Equal("Ctrl+Shift+Space", loaded.PttHotkey);
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
    public void Load_without_launchAtStartup_defaults_to_enabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"amr-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "masterEnabled": true,
                  "pttHotkey": "Ctrl+Shift+Space"
                }
                """);

            var loaded = new JsonSettingsStore(path).Load();

            Assert.True(loaded.LaunchAtStartup);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
