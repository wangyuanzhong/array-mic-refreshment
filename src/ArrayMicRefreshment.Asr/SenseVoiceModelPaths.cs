using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Asr;

public sealed record SenseVoiceModelPaths(string DirectoryPath, string TokensPath, string ModelPath, string ModelId);

public static class SenseVoiceModelResolver
{
    public const string PrimaryModelId = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09";
    public const string FallbackModelId = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17";

    public static SenseVoiceModelPaths Resolve(string modelsDirectory)
    {
        var root = ResolveModelsRoot(modelsDirectory);
        var primaryDir = Path.Combine(root, PrimaryModelId);
        if (TryResolveInDirectory(primaryDir, PrimaryModelId, out var primary))
        {
            return primary;
        }

        var fallbackDir = Path.Combine(root, FallbackModelId);
        if (TryResolveInDirectory(fallbackDir, FallbackModelId, out var fallback))
        {
            Log.Warning(
                "SenseVoice primary model {Primary} not found; using fallback {Fallback}. Run scripts/download-models.ps1 to install the primary package.",
                PrimaryModelId,
                FallbackModelId);
            return fallback;
        }

        throw new ModelNotFoundException(
            $"No SenseVoice model found under '{root}'. Expected '{PrimaryModelId}' or '{FallbackModelId}'. Run scripts/download-models.ps1.");
    }

    private static string ResolveModelsRoot(string modelsDirectory)
    {
        if (Path.IsPathRooted(modelsDirectory))
        {
            return modelsDirectory;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelsDirectory));
    }

    private static bool TryResolveInDirectory(string directory, string modelId, out SenseVoiceModelPaths paths)
    {
        paths = null!;
        if (!Directory.Exists(directory))
        {
            return false;
        }

        var tokens = Path.Combine(directory, "tokens.txt");
        if (!File.Exists(tokens))
        {
            return false;
        }

        var model = FindOnnxModel(directory);
        if (model is null)
        {
            return false;
        }

        paths = new SenseVoiceModelPaths(directory, tokens, model, modelId);
        return true;
    }

    private static string? FindOnnxModel(string directory)
    {
        var preferred = new[]
        {
            "model.int8.onnx",
            "model.onnx",
        };

        foreach (var name in preferred)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Directory.EnumerateFiles(directory, "*.onnx").OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();
    }
}
