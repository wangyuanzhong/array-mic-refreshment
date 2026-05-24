using System.Text.RegularExpressions;

namespace ArrayMicRefreshment.Asr;

/// <summary>
/// Lightweight local post-processor for ASR transcripts.
/// Removes filler words, fixes repeated punctuation, and does basic cleanup
/// without requiring cloud LLM.
/// </summary>
public static partial class SpeechCleaner
{
    /// <summary>Chinese filler words to remove: 嗯, 啊, 呃, 哦, 唉, 哼, 嘿, 哈, 哎, 哟, 哇, 咦, 啧, 呸, 嗷, 唔 etc.</summary>
    private static readonly HashSet<string> ChineseFillers = new(StringComparer.Ordinal)
    {
        "嗯", "啊", "呃", "哦", "唉", "哼", "嘿", "哈", "哎", "哟", "哇", "咦", "啧", "呸", "嗷", "唔",
        "嘛", "呢", "吧", "呗", "咧", "咯",
    };

    /// <summary>
    /// Filler chars that should always be removed regardless of context.
    /// These almost never appear as meaningful morphemes in spoken transcripts.
    /// </summary>
    private static readonly HashSet<string> AlwaysRemoveFillers = new(StringComparer.Ordinal)
    {
        "呃", "嗯", "啊",
    };

    [GeneratedRegex(@"[\s\,\.\;\:\!\?\，\。\；\：\！\?]+[\s\,\.\;\:\!\?\，\。\；\：\！\?]*", RegexOptions.Compiled)]
    private static partial Regex RepeatedPunctuationPattern();

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesPattern();

    [GeneratedRegex(@"^[\s\,\.\;\:\!\?\，\。\；\：\！\?]+", RegexOptions.Compiled)]
    private static partial Regex LeadingPunctuationPattern();

    [GeneratedRegex(@"[\s\,\.\;\:\!\?\，\。\；\：\！\?]+$", RegexOptions.Compiled)]
    private static partial Regex TrailingPunctuationPattern();

    /// <summary>
    /// Clean up ASR transcript: remove fillers, deduplicate punctuation, trim.
    /// </summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // Step 1: Remove standalone filler words (surrounded by spaces/punctuation or at boundaries)
        var cleaned = RemoveChineseFillers(text);

        // Step 2: Deduplicate repeated punctuation
        cleaned = RepeatedPunctuationPattern().Replace(cleaned, m =>
        {
            // Keep the first punctuation mark found
            var first = m.Value.FirstOrDefault(c => IsPunctuation(c));
            return first != default ? first.ToString() : " ";
        });

        // Step 3: Collapse multiple spaces
        cleaned = MultipleSpacesPattern().Replace(cleaned, " ");

        // Step 4: Trim leading/trailing punctuation and spaces
        cleaned = LeadingPunctuationPattern().Replace(cleaned, "");
        cleaned = TrailingPunctuationPattern().Replace(cleaned, "");

        return cleaned.Trim();
    }

    private static string RemoveChineseFillers(string text)
    {
        // Strategy: iterate character by character, skip filler chars that are
        // not part of a larger word. Filler chars standing alone or surrounded
        // by spaces/punctuation should be removed.
        var result = new System.Text.StringBuilder(text.Length);
        var chars = text.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            var ch = chars[i].ToString();

            if (!ChineseFillers.Contains(ch))
            {
                result.Append(chars[i]);
                continue;
            }

            // It's a filler char. Check if it's standalone (surrounded by space/punctuation/boundary).
            bool prevIsBoundary = i == 0 || IsBoundary(chars[i - 1]);
            bool nextIsBoundary = i == chars.Length - 1 || IsBoundary(chars[i + 1]);

            // Always remove the most common fillers regardless of context
            if (AlwaysRemoveFillers.Contains(ch))
            {
                // Skip this filler char, but add a space if both sides are word chars
                if (!prevIsBoundary || !nextIsBoundary)
                {
                    // At least one side is a word char - add a space to avoid concatenation
                    if (result.Length > 0 && !char.IsWhiteSpace(result[^1]))
                    {
                        result.Append(' ');
                    }
                }
                continue;
            }

            if (prevIsBoundary || nextIsBoundary)
            {
                // Skip this filler char, but add a space if both sides are word chars
                if (!prevIsBoundary || !nextIsBoundary)
                {
                    // Only one side is boundary - likely mid-sentence filler
                    // Add a space to avoid concatenation
                    if (result.Length > 0 && !char.IsWhiteSpace(result[^1]))
                    {
                        result.Append(' ');
                    }
                }
                // else both boundaries: pure standalone filler, just skip
                continue;
            }

            // Filler char is embedded inside a word (e.g., "嗯哼" as onomatopoeia) - keep it
            result.Append(chars[i]);
        }

        return result.ToString();
    }

    private static bool IsBoundary(char c) =>
        char.IsWhiteSpace(c) || IsPunctuation(c);

    private static bool IsPunctuation(char c) =>
        ",.;:!?，。；：！？".Contains(c);
}
