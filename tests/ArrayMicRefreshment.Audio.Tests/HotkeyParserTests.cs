using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class HotkeyParserTests
{
    [Theory]
    [InlineData("Ctrl+Shift+Space", true, true, false, false, 0x20u)]
    [InlineData("Alt+F1", false, false, true, false, 0x70u)]
    public void TryParse_ValidExpressions_ReturnChord(
        string expr,
        bool ctrl,
        bool shift,
        bool alt,
        bool win,
        uint vk)
    {
        var ok = HotkeyParser.TryParse(expr, out var chord, out var error);
        Assert.True(ok, error);
        Assert.NotNull(chord);
        Assert.Equal(ctrl, chord!.Ctrl);
        Assert.Equal(shift, chord.Shift);
        Assert.Equal(alt, chord.Alt);
        Assert.Equal(win, chord.Win);
        Assert.Equal(vk, chord.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("NotAKey")]
    [InlineData("Ctrl+Shift+Space+Extra")]
    public void TryParse_InvalidExpressions_ReturnFalse(string expr)
    {
        var ok = HotkeyParser.TryParse(expr, out var chord, out var error);
        Assert.False(ok);
        Assert.Null(chord);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
