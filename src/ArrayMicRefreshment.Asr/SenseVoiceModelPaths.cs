using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Asr;

public sealed record SenseVoiceModelPaths(string DirectoryPath, string TokensPath, string ModelPath, string ModelId);

public sealed record AsrModelInfo(string Id, string DisplayName, string Description, bool HasPunctuation, bool IsQuantized)
{
    public static readonly AsrModelInfo[] All =
    [
        new("sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
            "SenseVoice 2025-09 (int8) [粤语优化]",
            "针对粤语微调的量化模型，粤语识别更准，但不支持标点符号，中英文混杂性能一般。",
            false, true),
        new("sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17",
            "SenseVoice 2024-07 (int8) [推荐·通用]",
            "通用多语言量化模型，支持中英日韩粤，开启ITN后有标点符号，适合办公场景。",
            true, true),
        new("sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17",
            "SenseVoice 2024-07 (float32) [高精度]",
            "通用多语言高精度模型，无精度损失，准确率最高，但推理速度较慢、内存占用大。",
            true, false),
    ];

    public string DirectoryName => Id;
}

public static class SenseVoiceModelResolver
{
    public static IReadOnlyList<AsrModelInfo> ListAvailableModels(string modelsDirectory)
    {
        var allFound = new HashSet<string>();
        foreach (var root in ModelsPathResolver.GetAllCandidates(modelsDirectory))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var model in AsrModelInfo.All)
            {
                if (allFound.Contains(model.Id))
                {
                    continue;
                }

                var dir = Path.Combine(root, model.DirectoryName);
                if (TryResolveInDirectory(dir, model.Id, out _) ||
                    TryFindFuzzyMatch(root, model.Id, out _))
                {
                    allFound.Add(model.Id);
                }
            }
        }

        return AsrModelInfo.All.Where(m => allFound.Contains(m.Id)).ToArray();
    }

    public static SenseVoiceModelPaths Resolve(string modelsDirectory, string? preferredModelId = null)
    {
        var searchedRoots = new List<string>();

        foreach (var root in ModelsPathResolver.GetAllCandidates(modelsDirectory))
        {
            searchedRoots.Add(root);
            if (!Directory.Exists(root))
            {
                continue;
            }

            // If user has a preference, try exact match first, then fuzzy match
            if (!string.IsNullOrWhiteSpace(preferredModelId))
            {
                var preferredDir = Path.Combine(root, preferredModelId);
                if (TryResolveInDirectory(preferredDir, preferredModelId, out var preferred))
                {
                    Log.Information("Using preferred ASR model: {ModelId} from {Root}", preferredModelId, root);
                    return preferred;
                }

                // Fuzzy search: look for any subdirectory containing the model files
                if (TryFindFuzzyMatch(root, preferredModelId, out var fuzzy))
                {
                    Log.Warning(
                        "Preferred ASR model {Preferred} not found at exact path in {Root}, but found at '{ActualPath}'. Using it.",
                        preferredModelId,
                        root,
                        fuzzy.DirectoryPath);
                    return fuzzy;
                }
            }
            else
            {
                // Auto-select: try each model in priority order (only when user has NO preference)
                foreach (var modelInfo in AsrModelInfo.All)
                {
                    var dir = Path.Combine(root, modelInfo.DirectoryName);
                    if (TryResolveInDirectory(dir, modelInfo.Id, out var paths))
                    {
                        Log.Information("Auto-selected ASR model: {ModelId} from {Root}", modelInfo.Id, root);
                        return paths;
                    }

                    // Try fuzzy match for this model
                    if (TryFindFuzzyMatch(root, modelInfo.Id, out paths))
                    {
                        Log.Information("Auto-selected ASR model (fuzzy): {ModelId} at {Path} in {Root}", modelInfo.Id, paths.DirectoryPath, root);
                        return paths;
                    }
                }
            }
        }

        // Graceful fallback: if user-selected model not found, try to use any available model
        // instead of crashing. The settings UI already blocks saving uninstalled models.
        Log.Warning(
            "Preferred ASR model {Preferred} not found in any search path. Falling back to first available model. " +
            "Searched directories: {Searched}",
            preferredModelId,
            string.Join(", ", searchedRoots));

        foreach (var root in ModelsPathResolver.GetAllCandidates(modelsDirectory))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var modelInfo in AsrModelInfo.All)
            {
                var dir = Path.Combine(root, modelInfo.DirectoryName);
                if (TryResolveInDirectory(dir, modelInfo.Id, out var fallbackPaths))
                {
                    Log.Warning(
                        "Fallback to ASR model: {ModelId} at {Path} (user requested {Requested} was missing)",
                        modelInfo.Id,
                        fallbackPaths.DirectoryPath,
                        preferredModelId);
                    return fallbackPaths;
                }

                if (TryFindFuzzyMatch(root, modelInfo.Id, out fallbackPaths))
                {
                    Log.Warning(
                        "Fallback to ASR model (fuzzy): {ModelId} at {Path} (user requested {Requested} was missing)",
                        modelInfo.Id,
                        fallbackPaths.DirectoryPath,
                        preferredModelId);
                    return fallbackPaths;
                }
            }
        }

        throw new ModelNotFoundException(
            $"No SenseVoice model found. Searched directories:\n{string.Join("\n", searchedRoots.Select(r => "  - " + r))}\n\n" +
            $"Expected one of: {string.Join(", ", AsrModelInfo.All.Select(m => m.Id))}. " +
            "Run scripts/download-models.ps1.");
    }

    private static string ResolveModelsRoot(string modelsDirectory) =>
        ModelsPathResolver.Resolve(modelsDirectory);

    /// <summary>
    /// Attempts to find a model directory by searching all subdirectories in the root.
    /// Useful when tar extraction created a differently-named directory.
    /// </summary>
    private static bool TryFindFuzzyMatch(string root, string modelId, out SenseVoiceModelPaths paths)
    {
        paths = null!;
        if (!Directory.Exists(root))
        {
            return false;
        }

        // Search all immediate subdirectories
        foreach (var subDir in Directory.EnumerateDirectories(root))
        {
            var dirName = Path.GetFileName(subDir);
            // Skip hidden/system directories
            if (dirName.StartsWith('.') || dirName.Equals(".cache", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if directory name contains the model ID or vice versa
            var idMatch = dirName.Contains(modelId, StringComparison.OrdinalIgnoreCase) ||
                         modelId.Contains(dirName, StringComparison.OrdinalIgnoreCase);

            if (!idMatch)
            {
                // Also check if any .onnx file in the directory contains the model ID
                var onnxFiles = Directory.EnumerateFiles(subDir, "*.onnx").Select(Path.GetFileName);
                if (!onnxFiles.Any(f => f != null && f.Contains(modelId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            if (TryResolveInDirectory(subDir, modelId, out paths))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveInDirectory(string directory, string modelId, out SenseVoiceModelPaths paths)
    {
        paths = null!;
        if (!Directory.Exists(directory))
        {
            return false;
        }

        // Try direct resolution first
        if (TryResolveInDirectoryCore(directory, modelId, out paths))
        {
            return true;
        }

        // If the tar archive contained a single top-level directory, the extraction
        // may have created an extra nesting level. Search one level deeper.
        var subDirs = Directory.EnumerateDirectories(directory).ToList();
        if (subDirs.Count == 1)
        {
            var nested = subDirs[0];
            if (TryResolveInDirectoryCore(nested, modelId, out paths))
            {
                Log.Information(
                    "Resolved ASR model {ModelId} in nested directory: {NestedPath}",
                    modelId, nested);
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveInDirectoryCore(string directory, string modelId, out SenseVoiceModelPaths paths)
    {
        paths = null!;

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
