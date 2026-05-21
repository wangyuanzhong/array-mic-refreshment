namespace ArrayMicRefreshment.Audio;

public static class HotkeyParser
{
    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Enter"] = 0x0D,
        ["Tab"] = 0x09,
        ["Esc"] = 0x1B,
        ["Escape"] = 0x1B,
        ["Backspace"] = 0x08,
        ["Delete"] = 0x2E,
        ["Insert"] = 0x2D,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27,
    };

    public static bool TryParse(string? expression, out HotkeyChord? chord, out string? error)
    {
        chord = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Hotkey expression is empty.";
            return false;
        }

        var parts = expression.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = "Hotkey expression has no parts.";
            return false;
        }

        var ctrl = false;
        var shift = false;
        var alt = false;
        var win = false;
        uint? vk = null;

        foreach (var part in parts)
        {
            if (TryParseModifier(part, ref ctrl, ref shift, ref alt, ref win))
            {
                continue;
            }

            if (vk.HasValue)
            {
                error = $"Multiple key tokens: '{part}'.";
                return false;
            }

            if (!TryParseKeyToken(part, out var parsedVk))
            {
                error = $"Unrecognized key token: '{part}'.";
                return false;
            }

            vk = parsedVk;
        }

        if (!vk.HasValue)
        {
            error = "Hotkey expression must include a key (e.g. Space, F1, A).";
            return false;
        }

        chord = new HotkeyChord
        {
            Ctrl = ctrl,
            Shift = shift,
            Alt = alt,
            Win = win,
            VirtualKey = vk.Value,
        };
        return true;
    }

    private static bool TryParseModifier(string token, ref bool ctrl, ref bool shift, ref bool alt, ref bool win)
    {
        switch (token.ToLowerInvariant())
        {
            case "ctrl":
            case "control":
                ctrl = true;
                return true;
            case "shift":
                shift = true;
                return true;
            case "alt":
                alt = true;
                return true;
            case "win":
            case "windows":
            case "lwin":
            case "rwin":
                win = true;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseKeyToken(string token, out uint vk)
    {
        vk = 0;
        if (NamedKeys.TryGetValue(token, out vk))
        {
            return true;
        }

        if (token.Length == 2 && token[0] == 'F' && char.IsDigit(token[1]))
        {
            var n = token[1] - '0';
            if (n is >= 1 and <= 9)
            {
                vk = 0x6F + (uint)n;
                return true;
            }
        }

        if (token.Length == 3 && token.StartsWith('F') && int.TryParse(token[1..], out var fn) && fn is >= 1 and <= 24)
        {
            vk = 0x6F + (uint)fn;
            return true;
        }

        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                vk = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                vk = c;
                return true;
            }
        }

        return false;
    }
}
