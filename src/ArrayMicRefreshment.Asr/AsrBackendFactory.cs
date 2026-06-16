namespace ArrayMicRefreshment.Asr;

public static class AsrBackendFactory
{
    public static IOfflineAsrBackend Create(AsrModelPaths paths, int? numThreads = null)
    {
        var threads = numThreads ?? AsrInferenceTuning.ResolveNumThreads();
        return paths.Engine switch
        {
            AsrEngineKind.Qwen3Asr => new SherpaQwen3AsrBackend(paths, threads),
            AsrEngineKind.FireRedCtc => new SherpaFireRedCtcBackend(paths, threads),
            AsrEngineKind.SenseVoice => new SherpaSenseVoiceBackend(paths, threads),
            _ => throw new NotSupportedException($"Unsupported ASR engine: {paths.Engine}"),
        };
    }
}
