using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Integration.Tests.Support;

namespace ArrayMicRefreshment.Integration.Tests;

public sealed class SherpaRealModelTests
{
    [Fact]
    public async Task SenseVoice_real_model_decodes_when_enabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("INTEGRATION_REAL_MODELS"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("set INTEGRATION_REAL_MODELS=1 to enable Sherpa real-model integration test.");
            return;
        }

        var modelsDir = Path.Combine(RepoRoot.Find(), "models");
        if (AudioTestResources.TryFindRealSenseVoiceModelDirectory(modelsDir) is null)
        {
            Console.WriteLine($"SenseVoice model not found under {modelsDir}; skipping real-model test.");
            return;
        }

        using var asr = SenseVoiceAsr.CreateFromSettings(new AppSettings { ModelsDirectory = modelsDir });
        var utterance = AudioTestResources.CreateShortUtterance();
        var text = await asr.RecognizeUtteranceAsync(utterance, CancellationToken.None);
        var plain = SenseVoiceTextExtractor.ExtractPlainText(text);
        Assert.False(string.IsNullOrWhiteSpace(plain));
        Assert.DoesNotContain("<|", plain, StringComparison.Ordinal);
    }
}
