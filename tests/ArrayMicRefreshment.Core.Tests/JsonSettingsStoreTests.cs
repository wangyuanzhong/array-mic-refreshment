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
}
