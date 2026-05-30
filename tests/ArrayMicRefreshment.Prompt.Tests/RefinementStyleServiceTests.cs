using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Prompt.Tests;

public sealed class RefinementStyleServiceTests
{
    [Fact]
    public void List_includes_manifest_specialists()
    {
        var repoRoot = FindRepoRoot();
        var skillsDir = Path.Combine(repoRoot, "skills");

        var styles = RefinementStyleService.List(skillsDir);

        Assert.Contains(styles, s => s.Key == "plain-text" && !s.Deletable);
        Assert.Contains(styles, s => s.Key == "code-editing" && !s.Deletable);
    }

    [Fact]
    public void Add_and_delete_user_style_roundtrip()
    {
        var temp = Path.Combine(Path.GetTempPath(), "amr-style-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            CopySkillsTree(FindRepoRoot(), temp);
            var source = Path.Combine(Path.GetTempPath(), "amr-style-source.md");
            File.WriteAllText(
                source,
                """
                ---
                name: 测试风格
                description: 单元测试用
                id: test-style
                ---
                仅正文 prompt。
                """);

            var key = RefinementStyleService.AddFromSourceFile(temp, source);
            Assert.Equal("test-style", key);

            var listed = RefinementStyleService.List(temp);
            Assert.Contains(listed, s => s.Key == "test-style" && s.Deletable);

            RefinementStyleService.DeleteUserStyle(temp, "test-style");
            listed = RefinementStyleService.List(temp);
            Assert.DoesNotContain(listed, s => s.Key == "test-style");
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    private static void CopySkillsTree(string repoRoot, string dest)
    {
        var src = Path.Combine(repoRoot, "skills");
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "ArrayMicRefreshment.sln"))
                || File.Exists(Path.Combine(dir, "VERSION.txt")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
