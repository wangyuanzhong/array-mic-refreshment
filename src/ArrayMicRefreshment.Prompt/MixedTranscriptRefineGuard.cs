using System.Text.RegularExpressions;

namespace ArrayMicRefreshment.Prompt;

/// <summary>Local heuristics for mixed Chinese/English plain-text refine (no extra LLM calls).</summary>
public static partial class MixedTranscriptRefineGuard
{
    /// <summary>Minimum share of significant English tokens that must survive refine unchanged.</summary>
    public const double MinPreservedTokenRatio = 0.85;

    [GeneratedRegex(@"\p{IsBasicLatin}", RegexOptions.Compiled)]
    private static partial Regex BasicLatinPattern();

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9._/-]*", RegexOptions.Compiled)]
    private static partial Regex EnglishTokenPattern();

    public static bool LooksMixed(string? text) =>
        !string.IsNullOrWhiteSpace(text) && BasicLatinPattern().IsMatch(text);

    public static IReadOnlyList<string> ExtractEnglishTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokens = new List<string>();
        foreach (Match match in EnglishTokenPattern().Matches(text))
        {
            var token = match.Value;
            if (token.Length < 2 || !seen.Add(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    public static string AppendProtectionHint(string userContent, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return userContent;
        }

        var listed = string.Join(", ", tokens.Take(24));
        return userContent +
               "\n\n(Keep these Latin tokens exactly as written: " + listed + ")";
    }

    public static bool PassesValidation(string raw, string refined, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(refined))
        {
            return false;
        }

        if (!LooksMixed(raw))
        {
            return true;
        }

        var significant = tokens.Where(t => t.Length >= 2).ToList();
        if (significant.Count == 0)
        {
            return true;
        }

        var preserved = significant.Count(t => TokenPreserved(refined, t));
        return (double)preserved / significant.Count >= MinPreservedTokenRatio;
    }

    private static bool TokenPreserved(string refined, string token)
    {
        if (refined.Contains(token, StringComparison.Ordinal))
        {
            return true;
        }

        return refined.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
