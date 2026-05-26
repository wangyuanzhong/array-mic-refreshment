using System.Text;
using System.Text.Json;

namespace ArrayMicRefreshment.Asr;

/// <summary>Loads pre-encoded Sherpa ppinyin keyword lines shipped with the KWS model.</summary>
internal static class WakeWordKeywordEncodings
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string EncodingsFileName => "wake-phrase-encodings.json";

    public static bool TryGetEncodedLine(string modelRoot, string phrase, out string line)
    {
        line = string.Empty;
        var path = Path.Combine(modelRoot, EncodingsFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (map is null)
            {
                return false;
            }

            var key = phrase.Trim();
            if (key.Length == 0)
            {
                key = "小助手";
            }

            if (map.TryGetValue(key, out var encoded) && !string.IsNullOrWhiteSpace(encoded))
            {
                line = encoded.Trim();
                return true;
            }
        }
        catch
        {
            // fall through
        }

        return false;
    }

    public static void SaveEncodings(string modelRoot, IReadOnlyDictionary<string, string> lines)
    {
        var path = Path.Combine(modelRoot, EncodingsFileName);
        var json = JsonSerializer.Serialize(lines, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Utf8NoBom);
    }
}
