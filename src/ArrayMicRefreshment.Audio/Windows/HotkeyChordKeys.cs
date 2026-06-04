#if WINDOWS

namespace ArrayMicRefreshment.Audio;

/// <summary>PTT chord key classification for low-level hook suppression.</summary>
internal static class HotkeyChordKeys
{
    public static bool IsMainKey(HotkeyChord chord, uint virtualKey) =>
        chord.VirtualKey == virtualKey;
}

#endif
