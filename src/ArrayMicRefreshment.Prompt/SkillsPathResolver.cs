namespace ArrayMicRefreshment.Prompt;

public static class SkillsPathResolver
{
    public static string Resolve(string skillsDirectory)
    {
        var candidate = Path.GetFullPath(skillsDirectory);
        if (File.Exists(Path.Combine(candidate, "manifest.yaml")))
        {
            return candidate;
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

        return candidate;
    }
}
