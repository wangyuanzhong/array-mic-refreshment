namespace ArrayMicRefreshment.Asr;

public sealed record AsrModelInfo(
    string Id,
    string DisplayName,
    string Description,
    AsrEngineKind Engine,
    bool HasPunctuation,
    bool IsQuantized)
{
    public static readonly AsrModelInfo[] All =
    [
        new(
            "sherpa-onnx-fire-red-asr2-ctc-zh_en-int8-2026-02-25",
            "FireRedASR2 CTC (int8) [推荐·中英混说]",
            "传统 ASR，针对普通话 + 英文 + 中英 code-switch 优化；约 740 MB。纯中文与 SenseVoice 相当，频繁夹英文词时明显更好。",
            AsrEngineKind.FireRedCtc,
            false,
            true),
        new(
            "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17",
            "SenseVoice 2024-07 (int8) [通用]",
            "通用多语言量化模型，支持中英日韩粤，开启 ITN 后有标点，适合以中文为主的办公场景。",
            AsrEngineKind.SenseVoice,
            true,
            true),
        new(
            "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17",
            "SenseVoice 2024-07 (float32) [高精度]",
            "通用多语言高精度模型，纯中文略优，但英文/术语混说能力有限。",
            AsrEngineKind.SenseVoice,
            true,
            false),
        new(
            "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
            "SenseVoice 2025-09 (int8) [粤语优化]",
            "针对粤语微调的量化模型，粤语识别更准，但不支持标点，中英文混杂性能一般。",
            AsrEngineKind.SenseVoice,
            false,
            true),
    ];

    public string DirectoryName => Id;

    public static AsrModelInfo? TryGetById(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        return All.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }
}
