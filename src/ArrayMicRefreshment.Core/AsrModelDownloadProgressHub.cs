namespace ArrayMicRefreshment.Core;

/// <summary>Thread-safe progress snapshot for in-app ASR model downloads.</summary>
public static class AsrModelDownloadProgressHub
{
    private static readonly object Gate = new();
    private static DownloadProgress? _current;
    private static string? _activeModelId;

    public static void Begin(string modelId)
    {
        lock (Gate)
        {
            _activeModelId = modelId;
            _current = new DownloadProgress(0, "准备下载…", false);
        }
    }

    public static void Report(DownloadProgress progress)
    {
        lock (Gate)
        {
            _current = progress;
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _activeModelId = null;
            _current = null;
        }
    }

    public static AsrModelDownloadProgressSnapshot Snapshot()
    {
        lock (Gate)
        {
            return new AsrModelDownloadProgressSnapshot(
                _activeModelId,
                _current ?? new DownloadProgress(0, string.Empty, false));
        }
    }
}

public sealed record AsrModelDownloadProgressSnapshot(string? ModelId, DownloadProgress Progress);
