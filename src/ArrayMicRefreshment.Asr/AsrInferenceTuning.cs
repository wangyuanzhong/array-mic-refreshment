namespace ArrayMicRefreshment.Asr;

/// <summary>CPU offline ASR runtime tuning (all engines, no streaming).</summary>
public static class AsrInferenceTuning
{
    public const int MaxThreadCap = 8;
    public const int MinThreads = 2;

    /// <summary>Enough for ~200+ Chinese characters without truncating decoder output.</summary>
    public const int QwenMaxNewTokens = 512;

    public const int QwenMaxTotalLen = 512;

    /// <summary>0.1 s of silence at 16 kHz for ONNX session warmup.</summary>
    public const int WarmupSampleCount = 1600;

    public static int ResolveNumThreads()
    {
        return ResolveNumThreadsForProcessorCount(Environment.ProcessorCount);
    }

    public static int ResolveNumThreadsForProcessorCount(int logicalCores)
    {
        var half = Math.Max(MinThreads, logicalCores / 2);
        return Math.Min(MaxThreadCap, half);
    }
}
