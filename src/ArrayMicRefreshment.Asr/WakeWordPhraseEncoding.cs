using System.Text.RegularExpressions;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Asr;

/// <summary>Validates whether a wake phrase can be encoded for Sherpa KWS.</summary>
public static class WakeWordPhraseEncoding
{
    private static readonly Regex CjkOnly = new(@"^[\u4e00-\u9fff]+$", RegexOptions.Compiled);

    public static IReadOnlyList<string> BuiltinPhrases => WakeWordBuiltinEncodings.ListPhraseKeys();

    public static bool IsChineseOnly(string phrase)
    {
        var trimmed = phrase.Trim();
        return trimmed.Length > 0 && CjkOnly.IsMatch(trimmed);
    }

    /// <summary>
    /// Returns whether the phrase can be written to a Sherpa keywords file (builtin, cached, or Python).
    /// </summary>
    public static bool CanEncode(
        string modelsDirectory,
        string phrase,
        WakeWordSensitivity sensitivity,
        out string? errorMessage)
    {
        errorMessage = null;
        var trimmed = string.IsNullOrWhiteSpace(phrase) ? "小助手" : phrase.Trim();

        if (!IsChineseOnly(trimmed))
        {
            errorMessage = "唤醒词请仅使用中文汉字（不含空格、英文或标点）。";
            return false;
        }

        if (WakeWordBuiltinEncodings.TryGetLine(trimmed, out _))
        {
            return true;
        }

        if (!WakeWordModelPaths.TryResolve(modelsDirectory, out var paths))
        {
            // Model not installed yet — allow saving phrase; runtime will fail until KWS is downloaded.
            return true;
        }

        var modelRoot = Path.GetDirectoryName(paths!.TokensPath) ?? paths.TokensPath;
        WakeWordEncodingBootstrap.EnsureDefaultEncodings(modelRoot);

        if (WakeWordKeywordEncodings.TryGetEncodedLine(modelRoot, trimmed, out _))
        {
            return true;
        }

        var (score, threshold) = WakeWordSensitivityProfile.GetEncodingParams(sensitivity);
        var tempFile = Path.Combine(Path.GetTempPath(), $"amr-kw-probe-{Guid.NewGuid():N}.txt");
        try
        {
            if (WakeWordKeywordEncoder.TryWriteKeywordsFile(paths, trimmed, tempFile, score, threshold))
            {
                return true;
            }
        }
        finally
        {
            TryDelete(tempFile);
        }

        var builtins = string.Join("、", BuiltinPhrases);
        errorMessage =
            $"无法为「{trimmed}」生成 KWS 编码。请选用内置唤醒词：{builtins}；" +
            "或在本机安装 Python 与 sherpa-onnx（pip install sherpa-onnx）后重试，" +
            "或在仓库运行 scripts/generate-wake-encodings.ps1 将编码写入模型目录。";
        return false;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }
}
