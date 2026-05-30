namespace ArrayMicRefreshment.Prompt;

public static class SkillsPathResolver
{
    public static IReadOnlyList<string> GetAllCandidates(string skillsDirectory)
    {
        if (Path.IsPathRooted(skillsDirectory))
        {
            return new[] { skillsDirectory };
        }

        var baseDir = AppContext.BaseDirectory;
        return new[]
        {
            Path.Combine(baseDir, skillsDirectory),
            Path.GetFullPath(Path.Combine(baseDir, "..", skillsDirectory)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", skillsDirectory)),
        };
    }

    public static string Resolve(string skillsDirectory)
    {
        foreach (var candidate in GetAllCandidates(skillsDirectory))
        {
            if (File.Exists(Path.Combine(candidate, "manifest.yaml")))
            {
                return candidate;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var manifest = Path.Combine(dir.FullName, "skills", "manifest.yaml");
            if (File.Exists(manifest))
            {
                return Path.Combine(dir.FullName, "skills");
            }

            dir = dir.Parent;
        }

        return GetAllCandidates(skillsDirectory)[0];
    }

    /// <summary>
    /// Skills tree shipped beside the executable (<c>skills/manifest.yaml</c>), independent of user settings path.
    /// </summary>
    public static bool TryGetBundledSkillsRoot(out string bundledRoot)
    {
        foreach (var candidate in GetAllCandidates("skills"))
        {
            if (File.Exists(Path.Combine(candidate, "manifest.yaml")))
            {
                bundledRoot = candidate;
                return true;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var root = Path.Combine(dir.FullName, "skills");
            if (File.Exists(Path.Combine(root, "manifest.yaml")))
            {
                bundledRoot = root;
                return true;
            }

            dir = dir.Parent;
        }

        bundledRoot = string.Empty;
        return false;
    }
}
