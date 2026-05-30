using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class PttHotkeyInteractionTests
{
    [Fact]
    public void Hold_mode_starts_once_until_released_externally()
    {
        var active = false;
        var presses = 0;
        var releases = 0;

        PttHotkeyInteraction.OnHotkeyActivation(
            PttRecordingMode.Hold,
            ref active,
            () => presses++,
            () => releases++);
        PttHotkeyInteraction.OnHotkeyActivation(
            PttRecordingMode.Hold,
            ref active,
            () => presses++,
            () => releases++);

        Assert.True(active);
        Assert.Equal(1, presses);
        Assert.Equal(0, releases);

        active = false;
        releases++;
        Assert.Equal(1, presses);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void Toggle_mode_alternates_press_and_release()
    {
        var active = false;
        var presses = 0;
        var releases = 0;

        PttHotkeyInteraction.OnHotkeyActivation(
            PttRecordingMode.Toggle,
            ref active,
            () => presses++,
            () => releases++);
        Assert.True(active);
        Assert.Equal(1, presses);
        Assert.Equal(0, releases);

        PttHotkeyInteraction.OnHotkeyActivation(
            PttRecordingMode.Toggle,
            ref active,
            () => presses++,
            () => releases++);
        Assert.False(active);
        Assert.Equal(1, presses);
        Assert.Equal(1, releases);

        PttHotkeyInteraction.OnHotkeyActivation(
            PttRecordingMode.Toggle,
            ref active,
            () => presses++,
            () => releases++);
        Assert.True(active);
        Assert.Equal(2, presses);
        Assert.Equal(1, releases);
    }
}
