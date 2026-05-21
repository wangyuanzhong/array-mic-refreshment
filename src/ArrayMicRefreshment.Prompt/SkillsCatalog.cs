namespace ArrayMicRefreshment.Prompt;

public sealed class SkillsCatalog
{
    private readonly string _routerPromptPath;

    public SkillsManifestDocument Manifest { get; }
    public string SkillsRoot { get; }
    public string RouterSystemPrompt { get; }
    public IReadOnlyDictionary<string, string> FileContents { get; }
    public IReadOnlyList<string> MissingFiles { get; }
    public IReadOnlyList<OptionalSkillEntry> OptionalSkills { get; }

    private SkillsCatalog(
        SkillsManifestDocument manifest,
        string skillsRoot,
        string routerSystemPrompt,
        string routerPromptPath,
        Dictionary<string, string> fileContents,
        List<string> missingFiles,
        List<OptionalSkillEntry> optionalSkills)
    {
        Manifest = manifest;
        SkillsRoot = skillsRoot;
        RouterSystemPrompt = routerSystemPrompt;
        _routerPromptPath = routerPromptPath;
        FileContents = fileContents;
        MissingFiles = missingFiles;
        OptionalSkills = optionalSkills;
    }

    public static SkillsCatalog Load(string skillsDirectory)
    {
        var root = Path.GetFullPath(skillsDirectory);
        var manifestPath = Path.Combine(root, "manifest.yaml");
        var manifest = SkillsManifestLoader.LoadFromFile(manifestPath);

        var paths = CollectReferencedPaths(manifest);
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var relative in paths)
        {
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                missing.Add(relative);
                continue;
            }

            contents[relative] = File.ReadAllText(full);
        }

        if (!contents.TryGetValue(manifest.Router.SystemPromptFile, out var routerPrompt))
        {
            routerPrompt = string.Empty;
        }

        var optional = manifest.OptionalSkills
            .Select(kv => new OptionalSkillEntry(kv.Key, kv.Value.File, kv.Value.Note))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SkillsCatalog(
            manifest,
            root,
            routerPrompt,
            manifest.Router.SystemPromptFile,
            contents,
            missing,
            optional);
    }

    public string RequireRouterSystemPrompt()
    {
        if (string.IsNullOrEmpty(RouterSystemPrompt))
        {
            throw new InvalidOperationException(
                $"Router system prompt missing: {_routerPromptPath}. Missing files: {string.Join(", ", MissingFiles)}");
        }

        return RouterSystemPrompt;
    }

    public string ResolveStackContent(IEnumerable<string> stackPaths)
    {
        var parts = new List<string>();
        foreach (var path in stackPaths)
        {
            if (!FileContents.TryGetValue(path, out var text))
            {
                throw new InvalidOperationException($"Skill file not loaded: {path}");
            }

            parts.Add(text);
        }

        return string.Join("\n\n---\n\n", parts);
    }

    public string? TryResolveOptionalOverlay(IEnumerable<string> overlayKeys)
    {
        var parts = new List<string>();
        foreach (var key in overlayKeys)
        {
            if (!Manifest.OptionalSkills.TryGetValue(key, out var section))
            {
                continue;
            }

            if (!FileContents.TryGetValue(section.File, out var text))
            {
                throw new InvalidOperationException($"Optional skill file not loaded: {section.File} ({key})");
            }

            parts.Add(text);
        }

        return parts.Count == 0 ? null : string.Join("\n\n---\n\n", parts);
    }

    private static HashSet<string> CollectReferencedPaths(SkillsManifestDocument manifest)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            manifest.Router.SystemPromptFile,
        };

        foreach (var specialist in manifest.Specialists.Values)
        {
            foreach (var path in specialist.Stack)
            {
                set.Add(path);
            }
        }

        foreach (var optional in manifest.OptionalSkills.Values)
        {
            set.Add(optional.File);
        }

        return set;
    }

    public sealed record OptionalSkillEntry(string Key, string File, string? Note);
}
