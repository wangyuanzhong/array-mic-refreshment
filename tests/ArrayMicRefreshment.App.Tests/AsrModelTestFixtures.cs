using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

/// <summary>Creates minimal on-disk ASR model layouts so settings save validation passes in App.Tests.</summary>
internal static class AsrModelTestFixtures
{
    internal static string EnsureFireRedStubInstalled(AppSettings settings)
    {
        var model = AsrModelInfo.All[0];
        var root = Path.Combine(Path.GetTempPath(), "amr-test-models-" + Guid.NewGuid().ToString("N"));
        var extractDir = Path.Combine(root, model.DirectoryName);
        Directory.CreateDirectory(extractDir);
        File.WriteAllText(Path.Combine(extractDir, "tokens.txt"), "a");
        File.WriteAllText(Path.Combine(extractDir, "model.int8.onnx"), "fake");

        settings.ModelsDirectory = root;
        settings.SelectedAsrModelId = model.Id;
        return root;
    }
}
