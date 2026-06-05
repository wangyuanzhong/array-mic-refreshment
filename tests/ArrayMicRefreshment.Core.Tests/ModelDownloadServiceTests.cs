using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Core.Tests;

public class ModelDownloadServiceTests
{
    [Fact]
    public void IsPackageInstalled_false_when_directory_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "amr-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var package = new ModelPackage
            {
                Id = "test-model",
                ExtractDir = "missing-subdir",
                Archive = "missing.tar.bz2",
            };

            Assert.False(ModelDownloadService.IsPackageInstalled(dir, package));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsPackageInstalled_true_when_tokens_and_onnx_present()
    {
        var root = Path.Combine(Path.GetTempPath(), "amr-dl-" + Guid.NewGuid().ToString("N"));
        var extractDir = Path.Combine(root, "my-model");
        Directory.CreateDirectory(extractDir);
        File.WriteAllText(Path.Combine(extractDir, "tokens.txt"), "a");
        File.WriteAllText(Path.Combine(extractDir, "model.int8.onnx"), "fake");

        try
        {
            var package = new ModelPackage
            {
                Id = "my-model",
                ExtractDir = "my-model",
                Archive = "my-model.tar.bz2",
            };

            Assert.True(ModelDownloadService.IsPackageInstalled(root, package));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
