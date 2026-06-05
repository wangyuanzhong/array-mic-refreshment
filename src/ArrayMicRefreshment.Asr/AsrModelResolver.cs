using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Asr;

public static class AsrModelResolver
{
    public static IReadOnlyList<AsrModelInfo> ListInstalledModels(string modelsDirectory)
    {
        var allFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                if (TryResolveInDirectory(dir, model, out _) ||
                    TryFindFuzzyMatch(root, model, out _))
                {
                    allFound.Add(model.Id);
                }
            }
        }

        return AsrModelInfo.All.Where(m => allFound.Contains(m.Id)).ToArray();
    }

    public static bool IsModelInstalled(string modelsDirectory, string modelId)
    {
        var info = AsrModelInfo.TryGetById(modelId);
        if (info is null)
        {
            return false;
        }

        foreach (var root in ModelsPathResolver.GetAllCandidates(modelsDirectory))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var dir = Path.Combine(root, info.DirectoryName);
            if (TryResolveInDirectory(dir, info, out _) ||
                TryFindFuzzyMatch(root, info, out _))
            {
                return true;
            }
        }

        return false;
    }

    public static AsrModelPaths Resolve(string modelsDirectory, string? preferredModelId = null)
    {
        var searchedRoots = new List<string>();

        foreach (var root in ModelsPathResolver.GetAllCandidates(modelsDirectory))
        {
            searchedRoots.Add(root);
            if (!Directory.Exists(root))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(preferredModelId))
            {
                var preferredInfo = AsrModelInfo.TryGetById(preferredModelId);
                if (preferredInfo is not null)
                {
                    var preferredDir = Path.Combine(root, preferredInfo.DirectoryName);
                    if (TryResolveInDirectory(preferredDir, preferredInfo, out var preferred))
                    {
                        Log.Information("Using preferred ASR model: {ModelId} from {Root}", preferredModelId, root);
                        return preferred;
                    }

                    if (TryFindFuzzyMatch(root, preferredInfo, out var fuzzy))
                    {
                        Log.Warning(
                            "Preferred ASR model {Preferred} not found at exact path in {Root}, but found at '{ActualPath}'. Using it.",
                            preferredModelId,
                            root,
                            fuzzy.DirectoryPath);
                        return fuzzy;
                    }
                }
            }
            else
            {
                foreach (var modelInfo in AsrModelInfo.All)
                {
                    var dir = Path.Combine(root, modelInfo.DirectoryName);
                    if (TryResolveInDirectory(dir, modelInfo, out var paths))
                    {
                        Log.Information("Auto-selected ASR model: {ModelId} from {Root}", modelInfo.Id, root);
                        return paths;
                    }

                    if (TryFindFuzzyMatch(root, modelInfo, out paths))
                    {
                        Log.Information(
                            "Auto-selected ASR model (fuzzy): {ModelId} at {Path} in {Root}",
                            modelInfo.Id,
                            paths.DirectoryPath,
                            root);
                        return paths;
                    }
                }
            }
        }

        Log.Warning(
            "Preferred ASR model {Preferred} not found in any search path. Falling back to first available model. Searched: {Searched}",
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
                if (TryResolveInDirectory(dir, modelInfo, out var fallbackPaths))
                {
                    Log.Warning(
                        "Fallback ASR model: {ModelId} at {Path} (requested {Requested} missing)",
                        modelInfo.Id,
                        fallbackPaths.DirectoryPath,
                        preferredModelId);
                    return fallbackPaths;
                }

                if (TryFindFuzzyMatch(root, modelInfo, out fallbackPaths))
                {
                    Log.Warning(
                        "Fallback ASR model (fuzzy): {ModelId} at {Path} (requested {Requested} missing)",
                        modelInfo.Id,
                        fallbackPaths.DirectoryPath,
                        preferredModelId);
                    return fallbackPaths;
                }
            }
        }

        throw new ModelNotFoundException(
            $"No ASR model found. Searched directories:\n{string.Join("\n", searchedRoots.Select(r => "  - " + r))}\n\n" +
            $"Expected one of: {string.Join(", ", AsrModelInfo.All.Select(m => m.Id))}. " +
            "Download a model from Settings → ASR 模型, or run scripts/download-models.ps1.");
    }

    private static bool TryFindFuzzyMatch(string root, AsrModelInfo modelInfo, out AsrModelPaths paths)
    {
        paths = null!;
        if (!Directory.Exists(root))
        {
            return false;
        }

        foreach (var subDir in Directory.EnumerateDirectories(root))
        {
            var dirName = Path.GetFileName(subDir);
            if (dirName.StartsWith('.') || dirName.Equals(".cache", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var idMatch = dirName.Contains(modelInfo.Id, StringComparison.OrdinalIgnoreCase) ||
                          modelInfo.Id.Contains(dirName, StringComparison.OrdinalIgnoreCase);

            if (!idMatch)
            {
                var onnxFiles = Directory.EnumerateFiles(subDir, "*.onnx").Select(Path.GetFileName);
                if (!onnxFiles.Any(f => f != null && f.Contains(modelInfo.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            if (TryResolveInDirectory(subDir, modelInfo, out paths))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveInDirectory(string directory, AsrModelInfo modelInfo, out AsrModelPaths paths)
    {
        paths = null!;
        if (!Directory.Exists(directory))
        {
            return false;
        }

        if (TryResolveInDirectoryCore(directory, modelInfo, out paths))
        {
            return true;
        }

        var subDirs = Directory.EnumerateDirectories(directory).ToList();
        if (subDirs.Count == 1)
        {
            var nested = subDirs[0];
            if (TryResolveInDirectoryCore(nested, modelInfo, out paths))
            {
                Log.Information("Resolved ASR model {ModelId} in nested directory: {NestedPath}", modelInfo.Id, nested);
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveInDirectoryCore(string directory, AsrModelInfo modelInfo, out AsrModelPaths paths)
    {
        paths = null!;
        var tokens = Path.Combine(directory, "tokens.txt");
        if (!File.Exists(tokens))
        {
            return false;
        }

        var model = FindOnnxModel(directory, modelInfo.Engine);
        if (model is null)
        {
            return false;
        }

        paths = new AsrModelPaths(directory, tokens, model, modelInfo.Id, modelInfo.Engine);
        return true;
    }

    private static string? FindOnnxModel(string directory, AsrEngineKind engine)
    {
        var preferred = engine switch
        {
            AsrEngineKind.FireRedCtc => new[] { "model.int8.onnx", "model.onnx" },
            _ => new[] { "model.int8.onnx", "model.onnx" },
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

/// <summary>Backward-compatible wrapper.</summary>
public static class SenseVoiceModelResolver
{
    public static IReadOnlyList<AsrModelInfo> ListAvailableModels(string modelsDirectory) =>
        AsrModelResolver.ListInstalledModels(modelsDirectory);

    public static SenseVoiceModelPaths Resolve(string modelsDirectory, string? preferredModelId = null)
    {
        var paths = AsrModelResolver.Resolve(modelsDirectory, preferredModelId);
        return SenseVoiceModelPaths.From(paths);
    }
}
