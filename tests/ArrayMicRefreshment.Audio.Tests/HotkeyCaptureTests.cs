using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class HotkeyCaptureTests
{
    [Fact]
    public void TryBuildExpression_ShiftSpace_matches_parser()
    {
        var ok = HotkeyCapture.TryBuildExpression(
            control: false,
            shift: true,
            alt: false,
            win: false,
            virtualKey: 0x20,
            out var expression,
            out var error);

        Assert.True(ok, error);
        Assert.Equal("Shift+Space", expression);
        Assert.True(HotkeyParser.TryParse(expression, out var chord, out _));
        Assert.True(chord!.Shift);
        Assert.Equal(0x20u, chord.VirtualKey);
    }
}
