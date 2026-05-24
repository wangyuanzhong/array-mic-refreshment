namespace ArrayMicRefreshment.Audio;

/// <summary>Build hotkey expression strings from WinForms key events.</summary>
public static class HotkeyCapture
{
    public static bool TryBuildExpression(
        bool control,
        bool shift,
        bool alt,
        bool win,
        uint virtualKey,
        out string expression,
        out string? error)
    {
        expression = string.Empty;
        error = null;

        if (virtualKey == 0)
        {
            error = "需要包含主键（如 Space、F1、A）";
            return false;
        }

        var parts = new List<string>();
        if (control)
        {
            parts.Add("Ctrl");
        }

        if (shift)
        {
            parts.Add("Shift");
        }

        if (alt)
        {
            parts.Add("Alt");
        }

        if (win)
        {
            parts.Add("Win");
        }

        parts.Add(VirtualKeyToToken(virtualKey));
        expression = string.Join('+', parts);

        return HotkeyParser.TryParse(expression, out _, out error);
    }

    private static string VirtualKeyToToken(uint vk) => vk switch
    {
        0x20 => "Space",
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        _ => $"VK{vk:X}",
    };
}
