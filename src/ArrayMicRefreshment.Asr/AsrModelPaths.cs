namespace ArrayMicRefreshment.Asr;

public sealed record AsrModelPaths(
    string DirectoryPath,
    string TokensPath,
    string ModelPath,
    string ModelId,
    AsrEngineKind Engine,
    Qwen3AsrModelFiles? Qwen3 = null);

/// <summary>Backward-compatible alias for SenseVoice-only call sites.</summary>
public sealed record SenseVoiceModelPaths(string DirectoryPath, string TokensPath, string ModelPath, string ModelId)
{
    public static SenseVoiceModelPaths From(AsrModelPaths paths) =>
        new(paths.DirectoryPath, paths.TokensPath, paths.ModelPath, paths.ModelId);
}
