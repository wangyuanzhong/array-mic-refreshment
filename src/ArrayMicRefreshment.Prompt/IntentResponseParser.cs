using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArrayMicRefreshment.Prompt;

internal static class IntentResponseParser
{
    private static readonly Regex JsonFence = new(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParseIntentJson(string content, out string? intent, out float confidence)
    {
        intent = null;
        confidence = 0f;

        var trimmed = content.Trim();
        var fence = JsonFence.Match(trimmed);
        if (fence.Success)
        {
            trimmed = fence.Groups[1].Value.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.TryGetProperty("intent", out var intentEl))
            {
                intent = intentEl.GetString();
            }

            if (root.TryGetProperty("confidence", out var confEl) && confEl.TryGetSingle(out var c))
            {
                confidence = c;
            }
            else
            {
                confidence = intent is null ? 0f : 1f;
            }

            return !string.IsNullOrWhiteSpace(intent);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
