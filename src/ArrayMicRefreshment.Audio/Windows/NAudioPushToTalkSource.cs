#if WINDOWS
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>Global PTT hotkey via RegisterHotKey; release detected by polling key state.</summary>
public sealed class NAudioPushToTalkSource : IPushToTalkSource, IDisposable
{
    private readonly GlobalHotkeyListener _listener = new();
    private HotkeyChord? _chord;
    private int _releaseCount;

    public NAudioPushToTalkSource(string hotkeyExpression)
    {
        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out _))
        {
            chord = new HotkeyChord { Ctrl = true, Shift = true, VirtualKey = 0x20 };
        }

        _chord = chord;
        HotkeyDisplay = chord!.ToString();
        _listener.HotkeyPressed += (_, _) => PttPressed?.Invoke(this, EventArgs.Empty);
        _listener.HotkeyReleased += (_, _) =>
        {
            _releaseCount++;
            PttReleased?.Invoke(this, EventArgs.Empty);
        };
        _listener.TryRegister(chord!);
    }

    public event EventHandler? PttPressed;
    public event EventHandler? PttReleased;

    public string HotkeyDisplay { get; private set; }

    public int ReleaseEventCount => _releaseCount;

    public void UpdateHotkey(string hotkeyExpression)
    {
        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out _))
        {
            return;
        }

        _chord = chord;
        HotkeyDisplay = chord!.ToString();
        _listener.TryRegister(chord!);
    }

    /// <summary>Dev helper when hotkey is unavailable in headless environments.</summary>
    public void SimulatePress() => PttPressed?.Invoke(this, EventArgs.Empty);

    public void SimulateRelease()
    {
        _releaseCount++;
        PttReleased?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _listener.Dispose();
}
#endif
