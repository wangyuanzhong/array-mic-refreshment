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
        var modelId = AsrModelInfo.All[0].Id;
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
}
