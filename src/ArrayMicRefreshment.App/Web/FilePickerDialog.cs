namespace ArrayMicRefreshment.App.Web;

public sealed class FilePickerResultDto
{
    public string Path { get; set; } = string.Empty;

    public bool Cancelled { get; set; }
}

/// <summary>Native file picker for refinement-style Markdown files.</summary>
public static class FilePickerDialog
{
    public static FilePickerResultDto Show(IWin32Window? owner, string? initialPath)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择整理风格文档",
            Filter = "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            var path = initialPath.Trim();
            if (File.Exists(path))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(path);
                dialog.FileName = Path.GetFileName(path);
            }
            else if (Directory.Exists(path))
            {
                dialog.InitialDirectory = path;
            }
            else
            {
                var parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    dialog.InitialDirectory = parent;
                }
            }
        }

        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return new FilePickerResultDto
        {
            Path = dialog.FileName ?? string.Empty,
            Cancelled = result != DialogResult.OK,
        };
    }
}
