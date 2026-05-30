#if WINDOWS

namespace ArrayMicRefreshment.Audio;

/// <summary>HWND message sink for <see cref="NAudioPushToTalkSource"/> global RegisterHotKey.</summary>
public interface IGlobalHotkeyHost : IDisposable
{
    event EventHandler? HotkeyPressed;

    event EventHandler? HotkeyReleased;

    event Action<IntPtr>? ForegroundAtPress;

    event Action<IntPtr>? ForegroundAtRelease;

    bool IsRegistered { get; }

    bool TryRegister(HotkeyChord chord);

    void Unregister();

    void ResetHeldState();
}

#endif
