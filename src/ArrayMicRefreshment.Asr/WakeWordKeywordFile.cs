namespace ArrayMicRefreshment.Asr;

/// <summary>Writes Sherpa KWS keyword lines via <see cref="WakeWordKeywordEncoder"/>.</summary>
internal static class WakeWordKeywordFile
{
    public static bool TryWrite(WakeWordModelPaths paths, string filePath, string phrase)
        => WakeWordKeywordEncoder.TryWriteKeywordsFile(paths, phrase, filePath);
}
