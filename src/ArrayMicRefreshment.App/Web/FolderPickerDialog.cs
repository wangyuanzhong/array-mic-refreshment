namespace ArrayMicRefreshment.App.Web;

public sealed class FolderPickerResultDto
{
    public string Path { get; set; } = string.Empty;

    public bool Cancelled { get; set; }
}

/// <summary>Native folder picker for models/skills directories.</summary>
public static class FolderPickerDialog
{
    public static FolderPickerResultDto Show(IWin32Window? owner, string? initialPath)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            var path = initialPath.Trim();
            if (Directory.Exists(path))
            {
                dialog.SelectedPath = path;
            }
            else
            {
                var parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    dialog.SelectedPath = parent;
                }
            }
        }

        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return new FolderPickerResultDto
        {
            Path = dialog.SelectedPath ?? string.Empty,
            Cancelled = result != DialogResult.OK,
        };
    }
}
