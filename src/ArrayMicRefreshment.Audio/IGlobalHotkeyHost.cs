#if WINDOWS

using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>HWND message sink for <see cref="NAudioPushToTalkSource"/> global RegisterHotKey.</summary>
public interface IGlobalHotkeyHost : IDisposable
{
    event EventHandler? HotkeyPressed;

    event EventHandler? HotkeyReleased;

    event Action<IntPtr>? ForegroundAtPress;

    event Action<IntPtr>? ForegroundAtRelease;

    bool IsRegistered { get; }

    PttRecordingMode RecordingMode { get; set; }

    bool TryRegister(HotkeyChord chord);

    void Unregister();

    void ResetHeldState();
}

#endif
