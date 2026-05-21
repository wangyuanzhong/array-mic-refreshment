namespace ArrayMicRefreshment.Audio;

/// <summary>Parsed global hotkey chord for RegisterHotKey.</summary>
public sealed class HotkeyChord
{
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }
    public bool Win { get; init; }
    public uint VirtualKey { get; init; }

    public uint Modifiers
    {
        get
        {
            uint m = 0;
            if (Alt)
            {
                m |= 0x0001;
            }

            if (Ctrl)
            {
                m |= 0x0002;
            }

            if (Shift)
            {
                m |= 0x0004;
            }

            if (Win)
            {
                m |= 0x0008;
            }

            return m;
        }
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Win)
        {
            parts.Add("Win");
        }

        parts.Add(VirtualKeyToDisplay(VirtualKey));
        return string.Join('+', parts);
    }

    private static string VirtualKeyToDisplay(uint vk) => vk switch
    {
        0x20 => "Space",
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        _ => $"VK{vk:X}",
    };
}
