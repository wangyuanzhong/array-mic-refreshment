using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Speaker;

public sealed record SpeakerModelPaths(string ModelPath, string ModelId);

public static class SpeakerModelResolver
{
    public const string DefaultModelId = "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k";
    public const string DefaultOnnxFileName = "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx";

    public static SpeakerModelPaths Resolve(string modelsDirectory)
    {
        var root = ResolveModelsRoot(modelsDirectory);
        var candidates = new[]
        {
            Path.Combine(root, DefaultModelId, DefaultOnnxFileName),
            Path.Combine(root, DefaultModelId, "model.onnx"),
            Path.Combine(root, DefaultOnnxFileName),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return new SpeakerModelPaths(path, DefaultModelId);
            }
        }

        var dir = Path.Combine(root, DefaultModelId);
        if (Directory.Exists(dir))
        {
            var onnx = Directory.EnumerateFiles(dir, "*.onnx").FirstOrDefault();
            if (onnx is not null)
            {
                return new SpeakerModelPaths(onnx, DefaultModelId);
            }
        }

        throw new ModelNotFoundException(
            $"Speaker embedding model not found under '{root}'. Run scripts/download-models.ps1 -IncludeSpeaker.");
    }

    private static string ResolveModelsRoot(string modelsDirectory)
    {
        if (Path.IsPathRooted(modelsDirectory))
        {
            return modelsDirectory;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelsDirectory));
    }
}
