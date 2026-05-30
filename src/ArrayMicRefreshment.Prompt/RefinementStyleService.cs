using System.Text;
using System.Text.RegularExpressions;

namespace ArrayMicRefreshment.Prompt;

/// <summary>
/// Lists and manages refinement styles: built-in <c>manifest.yaml</c> specialists plus user files under
/// <c>refinement-styles/</c>.
/// </summary>
public static class RefinementStyleService
{
    public const string UserStylesSubfolder = "refinement-styles";

    public sealed record RefinementStyleEntry(
        string Key,
        string Name,
        string Description,
        bool Deletable,
        string? FileName);

    public static IReadOnlyList<RefinementStyleEntry> List(string skillsDirectory)
    {
        var entries = new List<RefinementStyleEntry>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Built-in specialists always come from the shipped skills tree (exe-adjacent skills/), not from refinement-styles/.
        string? bundledRoot = null;
        if (SkillsPathResolver.TryGetBundledSkillsRoot(out var bundled))
        {
            bundledRoot = bundled;
            TryAppendManifestSpecialists(bundled, entries, seenKeys);
        }

        var userRoot = ResolveUserSkillsRoot(skillsDirectory);
        if (!string.IsNullOrEmpty(userRoot)
            && (bundledRoot is null || !PathsEqual(userRoot, bundledRoot)))
        {
            TryAppendManifestSpecialists(userRoot, entries, seenKeys);
        }

        // User-added .md files live only under {Skills 目录}/refinement-styles/.
        if (!string.IsNullOrEmpty(userRoot))
        {
            AppendUserStyleFiles(userRoot, entries, seenKeys);
        }

        if (entries.Count > 0)
        {
            return entries;
        }

        return RefinementStyleDefaults.BuiltinEntries.ToList();
    }

    private static string ResolveUserSkillsRoot(string skillsDirectory)
    {
        foreach (var root in EnumerateSkillsRoots(skillsDirectory))
        {
            if (File.Exists(Path.Combine(root, "manifest.yaml"))
                || Directory.Exists(GetUserStylesDirectory(root)))
            {
                return root;
            }
        }

        try
        {
            return Path.GetFullPath(skillsDirectory.Trim());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path.GetFullPath(b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateSkillsRoots(string skillsDirectory)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var full = Path.GetFullPath(path);
                if (seen.Add(full))
                {
                    ordered.Add(full);
                }
            }
            catch
            {
                // ignore invalid paths
            }
        }

        foreach (var candidate in SkillsPathResolver.GetAllCandidates(skillsDirectory))
        {
            Add(candidate);
        }

        try
        {
            Add(SkillsPathResolver.Resolve(skillsDirectory));
        }
        catch
        {
            // ignore
        }

        return ordered;
    }

    private static bool TryAppendManifestSpecialists(
        string root,
        List<RefinementStyleEntry> entries,
        HashSet<string> seenKeys)
    {
        var manifestPath = Path.Combine(root, "manifest.yaml");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            var doc = SkillsManifestLoader.LoadFromFile(manifestPath);
            foreach (var (key, specialist) in doc.Specialists.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!seenKeys.Add(key))
                {
                    continue;
                }

                entries.Add(new RefinementStyleEntry(
                    key,
                    specialist.Name,
                    specialist.Description,
                    Deletable: false,
                    FileName: null));
            }

            return doc.Specialists.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void AppendUserStyleFiles(
        string root,
        List<RefinementStyleEntry> entries,
        HashSet<string> seenKeys)
    {
        var userDir = GetUserStylesDirectory(root);
        if (!Directory.Exists(userDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(userDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "manifest.yaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(file, Encoding.UTF8);
                var meta = RefinementStyleFrontmatter.Parse(text, Path.GetFileNameWithoutExtension(file));
                if (!seenKeys.Add(meta.Key))
                {
                    continue;
                }

                entries.Add(new RefinementStyleEntry(
                    meta.Key,
                    meta.Name,
                    meta.Description,
                    Deletable: true,
                    FileName: fileName));
            }
            catch
            {
                // skip unreadable files
            }
        }
    }

    public static string AddFromSourceFile(string skillsDirectory, string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("所选文件不存在。", sourceFilePath);
        }

        var ext = Path.GetExtension(sourceFilePath);
        if (!string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("仅支持 Markdown（.md）整理风格文件。");
        }

        var root = SkillsPathResolver.Resolve(skillsDirectory);
        var userDir = GetUserStylesDirectory(root);
        Directory.CreateDirectory(userDir);

        var sourceName = Path.GetFileName(sourceFilePath);
        var destName = EnsureUniqueFileName(userDir, sourceName);
        var destPath = Path.Combine(userDir, destName);
        File.Copy(sourceFilePath, destPath, overwrite: false);

        var text = File.ReadAllText(destPath, Encoding.UTF8);
        var meta = RefinementStyleFrontmatter.Parse(text, Path.GetFileNameWithoutExtension(destName));
        return meta.Key;
    }

    public static void DeleteUserStyle(string skillsDirectory, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("未指定整理风格。", nameof(key));
        }

        var root = SkillsPathResolver.Resolve(skillsDirectory);
        var userDir = GetUserStylesDirectory(root);
        if (!Directory.Exists(userDir))
        {
            throw new InvalidOperationException("未找到用户整理风格目录。");
        }

        foreach (var file in Directory.EnumerateFiles(userDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            var meta = RefinementStyleFrontmatter.Parse(text, Path.GetFileNameWithoutExtension(file));
            if (!string.Equals(meta.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(file);
            return;
        }

        throw new InvalidOperationException($"未找到可删除的整理风格「{key}」。");
    }

    public static bool TryBuildSystemPrompt(
        SkillsCatalog catalog,
        string specialistKey,
        IEnumerable<string> overlayKeys,
        out string prompt)
    {
        prompt = string.Empty;
        var key = specialistKey.Trim();

        if (catalog.Manifest.Specialists.TryGetValue(key, out var specialist))
        {
            try
            {
                var stackBody = catalog.ResolveStackContent(specialist.Stack);
                var overlay = catalog.TryResolveOptionalOverlay(overlayKeys);
                prompt = string.IsNullOrEmpty(overlay)
                    ? stackBody
                    : overlay + "\n\n---\n\n" + stackBody;
                return !string.IsNullOrWhiteSpace(prompt);
            }
            catch
            {
                return false;
            }
        }

        var userFile = FindUserStyleFile(catalog.SkillsRoot, key);
        if (userFile is null)
        {
            return false;
        }

        var text = File.ReadAllText(userFile, Encoding.UTF8);
        var meta = RefinementStyleFrontmatter.Parse(text, Path.GetFileNameWithoutExtension(userFile));
        if (meta.Stack.Count > 0)
        {
            try
            {
                var parts = new List<string>();
                foreach (var relative in meta.Stack)
                {
                    var full = Path.Combine(catalog.SkillsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(full))
                    {
                        throw new FileNotFoundException($"Skill 文件不存在: {relative}", full);
                    }

                    parts.Add(File.ReadAllText(full, Encoding.UTF8));
                }

                var stackBody = string.Join("\n\n---\n\n", parts);
                var overlay = catalog.TryResolveOptionalOverlay(overlayKeys);
                prompt = string.IsNullOrEmpty(overlay)
                    ? stackBody
                    : overlay + "\n\n---\n\n" + stackBody;
                return !string.IsNullOrWhiteSpace(prompt);
            }
            catch
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(meta.Body))
        {
            var overlay = catalog.TryResolveOptionalOverlay(overlayKeys);
            prompt = string.IsNullOrEmpty(overlay)
                ? meta.Body
                : overlay + "\n\n---\n\n" + meta.Body;
            return true;
        }

        return false;
    }

    public static string GetUserStylesDirectory(string skillsRoot) =>
        Path.Combine(Path.GetFullPath(skillsRoot), UserStylesSubfolder);

    private static string? FindUserStyleFile(string skillsRoot, string key)
    {
        var userDir = GetUserStylesDirectory(skillsRoot);
        if (!Directory.Exists(userDir))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(userDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            var meta = RefinementStyleFrontmatter.Parse(text, Path.GetFileNameWithoutExtension(file));
            if (string.Equals(meta.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    private static string EnsureUniqueFileName(string directory, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var candidate = fileName;
        var n = 1;
        while (File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{baseName}-{n}{ext}";
            n++;
        }

        return candidate;
    }
}

internal static partial class RefinementStyleFrontmatter
{
    public sealed record ParsedStyle(string Key, string Name, string Description, IReadOnlyList<string> Stack, string Body);

    public static ParsedStyle Parse(string markdown, string fallbackKeyStem)
    {
        var (frontmatter, body) = SplitFrontmatter(markdown);
        var fields = ParseSimpleYaml(frontmatter);

        var key = fields.GetValueOrDefault("id")
            ?? fields.GetValueOrDefault("key")
            ?? SanitizeKey(fallbackKeyStem);
        var name = fields.GetValueOrDefault("name") ?? fallbackKeyStem;
        var description = fields.GetValueOrDefault("description") ?? string.Empty;

        var stack = new List<string>();
        if (fields.TryGetValue("stack", out var stackRaw) && !string.IsNullOrWhiteSpace(stackRaw))
        {
            foreach (var line in stackRaw.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                var item = line.Trim().TrimStart('-').Trim();
                if (!string.IsNullOrWhiteSpace(item))
                {
                    stack.Add(item);
                }
            }
        }

        return new ParsedStyle(key, name, description, stack, body.Trim());
    }

    private static (string Frontmatter, string Body) SplitFrontmatter(string markdown)
    {
        if (!markdown.StartsWith("---", StringComparison.Ordinal))
        {
            return (string.Empty, markdown);
        }

        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return (string.Empty, markdown);
        }

        var fm = markdown[4..end].Trim();
        var body = markdown[(end + 4)..].TrimStart();
        if (body.StartsWith("---", StringComparison.Ordinal))
        {
            body = body[3..].TrimStart();
        }

        return (fm, body);
    }

    private static Dictionary<string, string> ParseSimpleYaml(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return result;
        }

        var lines = yaml.Split(['\n', '\r']);
        string? currentKey = null;
        var stackBuilder = new StringBuilder();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("stack:", StringComparison.OrdinalIgnoreCase))
            {
                currentKey = "stack";
                stackBuilder.Clear();
                var inline = line["stack:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(inline))
                {
                    stackBuilder.AppendLine(inline);
                }

                continue;
            }

            if (currentKey == "stack")
            {
                if (line.StartsWith('-'))
                {
                    stackBuilder.AppendLine(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                result["stack"] = stackBuilder.ToString().Trim();
                currentKey = null;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"');
            result[key] = value;
        }

        if (currentKey == "stack" && stackBuilder.Length > 0)
        {
            result["stack"] = stackBuilder.ToString().Trim();
        }

        return result;
    }

    private static string SanitizeKey(string stem)
    {
        var s = stem.Trim().ToLowerInvariant();
        s = InvalidKeyChars().Replace(s, "-");
        s = Regex.Replace(s, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(s) ? "custom-style" : s;
    }

    [GeneratedRegex(@"[^a-z0-9\-]+")]
    private static partial Regex InvalidKeyChars();
}
