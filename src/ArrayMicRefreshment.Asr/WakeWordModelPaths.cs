using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Asr;

/// <summary>Paths to Sherpa-ONNX Zipformer KWS (WenetSpeech) model files under <c>models/</c>.</summary>
public sealed class WakeWordModelPaths
{
    public const string ModelDirName = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01";

    public required string TokensPath { get; init; }
    public required string EncoderPath { get; init; }
    public required string DecoderPath { get; init; }
    public required string JoinerPath { get; init; }

    public static bool TryResolve(string modelsDirectory, out WakeWordModelPaths paths)
    {
        foreach (var root in ModelsPathResolver.GetAllCandidates(modelsDirectory))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var candidate = BuildPaths(Path.Combine(root, ModelDirName));
            if (FilesExist(candidate))
            {
                paths = candidate;
                return true;
            }
        }

        paths = BuildPaths(Path.Combine(ModelsPathResolver.Resolve(modelsDirectory), ModelDirName));
        return false;
    }

    private static WakeWordModelPaths BuildPaths(string root) => new()
    {
        TokensPath = Path.Combine(root, "tokens.txt"),
        EncoderPath = Path.Combine(root, "encoder-epoch-12-avg-2-chunk-16-left-64.onnx"),
        DecoderPath = Path.Combine(root, "decoder-epoch-12-avg-2-chunk-16-left-64.onnx"),
        JoinerPath = Path.Combine(root, "joiner-epoch-12-avg-2-chunk-16-left-64.onnx"),
    };

    private static bool FilesExist(WakeWordModelPaths paths) =>
        File.Exists(paths.TokensPath)
        && File.Exists(paths.EncoderPath)
        && File.Exists(paths.DecoderPath)
        && File.Exists(paths.JoinerPath);
}
