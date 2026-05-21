using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Prompt.Tests;

public class SkillsManifestTests
{
    private static string RepoSkillsDir()
    {
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

        throw new InvalidOperationException("Could not locate skills/manifest.yaml from test output.");
    }

    [Fact]
    public void Load_valid_manifest_succeeds()
    {
        var catalog = SkillsCatalog.Load(RepoSkillsDir());
        Assert.False(string.IsNullOrWhiteSpace(catalog.RequireRouterSystemPrompt()));
        Assert.Contains("write_code", catalog.Manifest.Router.IntentMap.Keys);
        Assert.Empty(catalog.MissingFiles);
    }

    [Fact]
    public void Load_missing_manifest_throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.Throws<FileNotFoundException>(() => SkillsManifestLoader.LoadFromFile(Path.Combine(dir, "manifest.yaml")));
    }

    [Fact]
    public void Load_manifest_with_missing_upstream_files_reports_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"amr-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var manifest = """
            version: 1
            router:
              system_prompt_file: upstream/missing-router.md
              intent_map:
                write_code: code-editing
            specialists:
              code-editing:
                stack:
                  - upstream/missing-stack.md
            """;
        File.WriteAllText(Path.Combine(root, "manifest.yaml"), manifest);
        try
        {
            var catalog = SkillsCatalog.Load(root);
            Assert.Contains("upstream/missing-router.md", catalog.MissingFiles);
            Assert.Throws<InvalidOperationException>(() => catalog.RequireRouterSystemPrompt());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
