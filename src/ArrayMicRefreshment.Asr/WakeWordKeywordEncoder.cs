using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace ArrayMicRefreshment.Asr;

/// <summary>Builds Sherpa ppinyin keyword lines for the Wenetspeech KWS model.</summary>
internal static class WakeWordKeywordEncoder
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly Regex CjkOnly = new(@"^[\u4e00-\u9fff]+$", RegexOptions.Compiled);

    public static bool TryWriteKeywordsFile(
        WakeWordModelPaths paths,
        string phrase,
        string outputPath,
        float score,
        float threshold)
    {
        var trimmed = string.IsNullOrWhiteSpace(phrase) ? "小助手" : phrase.Trim();
        var modelRoot = Path.GetDirectoryName(paths.TokensPath) ?? paths.TokensPath;

        // Always regenerate with the active score/threshold — cached model encodings use #0.30
        // which makes wake far harder than KeywordsThreshold in spotter config suggests.
        if (TryGenerateViaPython(paths.TokensPath, trimmed, score, threshold, out var generated))
        {
            WakeWordKeywordEncodings.SaveEncodings(
                modelRoot,
                new Dictionary<string, string> { [trimmed] = generated });
            WriteLine(outputPath, generated);
            Log.Information(
                "[WAKE-DIAG] wake keyword line (generated) phrase={Phrase} score={Score:F1} threshold={Threshold:F3} line={Line}",
                trimmed,
                score,
                threshold,
                generated);
            return true;
        }

        if (WakeWordKeywordEncodings.TryGetEncodedLine(modelRoot, trimmed, out var cached))
        {
            WriteLine(outputPath, cached);
            Log.Warning(
                "[WAKE-DIAG] wake keyword line (cached, threshold may be high) phrase={Phrase} line={Line}. " +
                "Install Python+sherpa_onnx to regenerate with threshold={Threshold:F3}.",
                trimmed,
                cached,
                threshold);
            return true;
        }

        Log.Warning(
            "[WAKE-DIAG] wake phrase encode failed phrase={Phrase}. Use Chinese characters only, or run scripts/generate-wake-encodings.ps1.",
            trimmed);
        Log.Warning(
            "Cannot encode wake phrase '{Phrase}' for Sherpa KWS. " +
            "Use Chinese characters only, or run scripts/generate-wake-encodings.ps1 and repackage.",
            trimmed);
        return false;
    }

    private static void WriteLine(string outputPath, string line)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, line + Environment.NewLine, Utf8NoBom);
    }

    private static bool TryGenerateViaPython(
        string tokensPath,
        string phrase,
        float score,
        float threshold,
        out string line)
    {
        line = string.Empty;
        if (!CjkOnly.IsMatch(phrase))
        {
            return false;
        }

        var python = FindPython();
        if (python is null)
        {
            return false;
        }

        var rawPath = Path.Combine(Path.GetTempPath(), $"amr-kw-raw-{Guid.NewGuid():N}.txt");
        var outPath = Path.Combine(Path.GetTempPath(), $"amr-kw-out-{Guid.NewGuid():N}.txt");
        try
        {
            var atPhrase = phrase.Replace(" ", "_", StringComparison.Ordinal);
            var rawLine = $"{phrase} :{score.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"#{threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)} @{atPhrase}";
            File.WriteAllText(rawPath, rawLine + Environment.NewLine, Utf8NoBom);

            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments =
                    $"-m sherpa_onnx.cli text2token --tokens \"{tokensPath}\" --tokens-type ppinyin \"{rawPath}\" \"{outPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return false;
            }

            proc.WaitForExit(15_000);
            if (proc.ExitCode != 0 || !File.Exists(outPath))
            {
                return false;
            }

            var encoded = File.ReadAllLines(outPath, Encoding.UTF8)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0);
            if (string.IsNullOrEmpty(encoded))
            {
                return false;
            }

            line = encoded;
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Python text2token failed for wake phrase");
            return false;
        }
        finally
        {
            TryDelete(rawPath);
            TryDelete(outPath);
        }
    }

    private static string? FindPython()
    {
        foreach (var name in new[] { "python", "python3" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "-c \"import sherpa_onnx\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi);
                if (proc is null)
                {
                    continue;
                }

                proc.WaitForExit(3000);
                if (proc.ExitCode == 0)
                {
                    return name;
                }
            }
            catch
            {
                // try next
            }
        }

        return null;
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
