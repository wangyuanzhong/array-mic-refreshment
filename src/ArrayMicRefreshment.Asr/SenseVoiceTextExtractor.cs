using System.Text.RegularExpressions;

namespace ArrayMicRefreshment.Asr;

/// <summary>Strips SenseVoice rich tags (emotion, event, language) from recognizer output.</summary>
public static partial class SenseVoiceTextExtractor
{
    [GeneratedRegex(@"<\|[^|]+\|>", RegexOptions.Compiled)]
    private static partial Regex SenseVoiceTagPattern();

    public static string ExtractPlainText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var stripped = SenseVoiceTagPattern().Replace(raw, string.Empty);
        return stripped.Trim();
    }
}
