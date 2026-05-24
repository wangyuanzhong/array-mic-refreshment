namespace ArrayMicRefreshment.Core;

/// <summary>Resolve <see cref="AppSettings.ModelsDirectory"/> next to the running executable.</summary>
public static class ModelsPathResolver
{
    /// <summary>Returns all candidate paths where the models directory might exist.</summary>
    public static IReadOnlyList<string> GetAllCandidates(string modelsDirectory)
    {
        if (Path.IsPathRooted(modelsDirectory))
        {
            return new[] { modelsDirectory };
        }

        var baseDir = AppContext.BaseDirectory;
        return new[]
        {
            Path.Combine(baseDir, modelsDirectory),
            Path.GetFullPath(Path.Combine(baseDir, "..", modelsDirectory)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", modelsDirectory)),
        };
    }

    /// <summary>Returns the first candidate that exists on disk.</summary>
    public static string Resolve(string modelsDirectory)
    {
        foreach (var candidate in GetAllCandidates(modelsDirectory))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fallback: return the primary candidate even if it doesn't exist yet
        return GetAllCandidates(modelsDirectory)[0];
    }
}
