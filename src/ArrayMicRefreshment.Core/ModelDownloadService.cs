using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArrayMicRefreshment.Core;

public sealed class ModelDownloadService
{
    private readonly HttpClient _httpClient;

    public ModelDownloadService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadModelAsync(
        string modelsDirectory,
        string packageId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var manifest = await LoadManifestAsync(cancellationToken);
        var package = manifest.Packages.FirstOrDefault(p => p.Id == packageId)
            ?? throw new InvalidOperationException($"Model package '{packageId}' not found in manifest.");

        var root = ModelsPathResolver.Resolve(modelsDirectory);
        var cacheDir = Path.Combine(root, ".cache");
        Directory.CreateDirectory(cacheDir);

        var archivePath = Path.Combine(cacheDir, package.Archive);
        var tempArchivePath = archivePath + ".tmp";
        var extractDir = Path.Combine(root, package.ExtractDir);

        // Clean up any stale incomplete downloads
        if (File.Exists(tempArchivePath))
        {
            File.Delete(tempArchivePath);
        }

        // If an existing archive exists but is likely incomplete (no SHA256 to verify),
        // delete it and re-download to be safe
        if (File.Exists(archivePath) && string.IsNullOrEmpty(package.Sha256))
        {
            var info = new FileInfo(archivePath);
            // If file is older than 1 hour, assume it's stale/interrupted
            if (info.LastWriteTime < DateTime.Now.AddHours(-1))
            {
                File.Delete(archivePath);
            }
        }

        // Download
        if (!File.Exists(archivePath))
        {
            var url = $"{manifest.BaseUrl.TrimEnd('/')}/{package.Archive}";
            progress?.Report(new DownloadProgress(0, $"正在下载 {packageId}...", false));

            await DownloadFileAsync(url, tempArchivePath, progress, cancellationToken);

            // Atomic rename to avoid leaving incomplete files
            File.Move(tempArchivePath, archivePath);
        }

        // Verify SHA256 if available
        if (!string.IsNullOrEmpty(package.Sha256))
        {
            progress?.Report(new DownloadProgress(99, "校验文件...", false));
            var hash = await ComputeSha256Async(archivePath, cancellationToken);
            if (!string.Equals(hash, package.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(archivePath);
                throw new InvalidOperationException(
                    $"SHA256 mismatch for {packageId}. Expected {package.Sha256}, got {hash}.");
            }
        }

        // Extract
        progress?.Report(new DownloadProgress(99, "正在解压...", false));
        ExtractArchive(archivePath, root);

        // Cleanup
        File.Delete(archivePath);
        progress?.Report(new DownloadProgress(100, $"✓ {packageId} 安装完成", true));
    }

    private static async Task<ModelManifest> LoadManifestAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(ModelDownloadService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("ModelManifest.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            return await JsonSerializer.DeserializeAsync<ModelManifest>(stream, cancellationToken: cancellationToken)
                ?? new ModelManifest();
        }

        // Fallback: load from file system
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "ModelManifest.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "scripts", "ModelManifest.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "scripts", "ModelManifest.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonSerializer.Deserialize<ModelManifest>(json) ?? new ModelManifest();
            }
        }

        throw new FileNotFoundException("ModelManifest.json not found.");
    }

    private async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long readBytes = 0;
        int bytesRead;

        var sw = Stopwatch.StartNew();
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            readBytes += bytesRead;

            if (totalBytes > 0 && sw.ElapsedMilliseconds > 500)
            {
                var percent = (int)((readBytes * 100) / totalBytes);
                var mb = readBytes / (1024.0 * 1024.0);
                var totalMb = totalBytes / (1024.0 * 1024.0);
                progress?.Report(new DownloadProgress(percent, $"下载中 {mb:F1}/{totalMb:F1} MB ({percent}%)", false));
                sw.Restart();
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void ExtractArchive(string archivePath, string destination)
    {
        if (archivePath.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
        {
            // Use tar command (available on Windows 10 1803+)
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xjf \"{archivePath}\" -C \"{destination}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start tar process.");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"tar extraction failed: {error}");
            }
        }
        else if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: true);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported archive format: {archivePath}");
        }
    }
}

public sealed record DownloadProgress(int Percent, string Message, bool IsComplete);

public sealed class ModelManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("packages")]
    public List<ModelPackage> Packages { get; set; } = new();
}

public sealed class ModelPackage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("archive")]
    public string Archive { get; set; } = string.Empty;

    [JsonPropertyName("extractDir")]
    public string ExtractDir { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
