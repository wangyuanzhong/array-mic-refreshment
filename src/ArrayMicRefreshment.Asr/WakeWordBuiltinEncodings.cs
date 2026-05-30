using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ArrayMicRefreshment.Asr;

/// <summary>Shipped ppinyin lines for default wake phrases (no Python at runtime).</summary>
internal static class WakeWordBuiltinEncodings
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static IReadOnlyDictionary<string, string>? _cache;

    public static IReadOnlyList<string> ListPhraseKeys()
    {
        var map = Load();
        return map.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    public static bool TryGetLine(string phrase, out string line)
    {
        line = string.Empty;
        var map = Load();
        var key = string.IsNullOrWhiteSpace(phrase) ? "小助手" : phrase.Trim();
        if (map.TryGetValue(key, out var encoded) && !string.IsNullOrWhiteSpace(encoded))
        {
            line = encoded.Trim();
            return true;
        }

        return false;
    }

    public static void EnsureCopiedToModelRoot(string modelRoot)
    {
        var target = Path.Combine(modelRoot, WakeWordKeywordEncodings.EncodingsFileName);
        if (File.Exists(target))
        {
            return;
        }

        var map = Load();
        if (map.Count == 0)
        {
            return;
        }

        WakeWordKeywordEncodings.SaveEncodings(modelRoot, map);
    }

    private static IReadOnlyDictionary<string, string> Load()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        try
        {
            var asm = typeof(WakeWordBuiltinEncodings).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("wake-phrase-encodings.json", StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                _cache = new Dictionary<string, string>();
                return _cache;
            }

            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null)
            {
                _cache = new Dictionary<string, string>();
                return _cache;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = reader.ReadToEnd();
            _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                     ?? new Dictionary<string, string>();
            return _cache;
        }
        catch
        {
            _cache = new Dictionary<string, string>();
            return _cache;
        }
    }
}
