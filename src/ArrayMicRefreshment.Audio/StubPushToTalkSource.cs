using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>Phase 1: replace with NAudio capture + global hotkey.</summary>
public sealed class StubPushToTalkSource : IPushToTalkSource
{
    public event EventHandler? PttPressed;
    public event EventHandler? PttReleased;

    public string HotkeyDisplay { get; set; } = "Ctrl+Shift+Space";

    public void SimulatePress() => PttPressed?.Invoke(this, EventArgs.Empty);

    public void SimulateRelease() => PttReleased?.Invoke(this, EventArgs.Empty);
}
