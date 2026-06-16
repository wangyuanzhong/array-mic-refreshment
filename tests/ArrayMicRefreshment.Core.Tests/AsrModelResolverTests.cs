using ArrayMicRefreshment.Asr;

namespace ArrayMicRefreshment.Core.Tests;

public class AsrModelResolverTests
{
    [Fact]
    public void ListInstalledModels_empty_when_no_models()
    {
        var dir = Path.Combine(Path.GetTempPath(), "amr-asr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            Assert.Empty(AsrModelResolver.ListInstalledModels(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsModelInstalled_true_for_fire_red_layout()
    {
        var root = Path.Combine(Path.GetTempPath(), "amr-asr-" + Guid.NewGuid().ToString("N"));
        var modelId = "sherpa-onnx-fire-red-asr2-ctc-zh_en-int8-2026-02-25";
        var extractDir = Path.Combine(root, modelId);
        Directory.CreateDirectory(extractDir);
        File.WriteAllText(Path.Combine(extractDir, "tokens.txt"), "a");
        File.WriteAllText(Path.Combine(extractDir, "model.int8.onnx"), "fake");

        try
        {
            Assert.True(AsrModelResolver.IsModelInstalled(root, modelId));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsModelInstalled_true_for_qwen3_layout()
    {
        var root = Path.Combine(Path.GetTempPath(), "amr-asr-" + Guid.NewGuid().ToString("N"));
        var modelId = "sherpa-onnx-qwen3-asr-0.6B-int8-2026-03-25";
        var extractDir = Path.Combine(root, modelId);
        Directory.CreateDirectory(extractDir);
        Directory.CreateDirectory(Path.Combine(extractDir, "tokenizer"));
        File.WriteAllText(Path.Combine(extractDir, "conv_frontend.onnx"), "fake");
        File.WriteAllText(Path.Combine(extractDir, "encoder.int8.onnx"), "fake");
        File.WriteAllText(Path.Combine(extractDir, "decoder.int8.onnx"), "fake");

        try
        {
            Assert.True(AsrModelResolver.IsModelInstalled(root, modelId));
            var resolved = AsrModelResolver.Resolve(root, modelId);
            Assert.Equal(AsrEngineKind.Qwen3Asr, resolved.Engine);
            Assert.NotNull(resolved.Qwen3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
