namespace ArrayMicRefreshment.Asr;

public static class AsrBackendFactory
{
    public static IOfflineAsrBackend Create(AsrModelPaths paths, int numThreads = 2) =>
        paths.Engine switch
        {
            AsrEngineKind.FireRedCtc => new SherpaFireRedCtcBackend(paths, numThreads),
            AsrEngineKind.SenseVoice => new SherpaSenseVoiceBackend(paths, numThreads),
            _ => throw new NotSupportedException($"Unsupported ASR engine: {paths.Engine}"),
        };
}
