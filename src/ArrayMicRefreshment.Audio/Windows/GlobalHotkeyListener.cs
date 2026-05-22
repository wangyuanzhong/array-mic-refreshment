#if WINDOWS
using System.Runtime.InteropServices;

namespace ArrayMicRefreshment.Audio;

/// <summary>Hidden message window for RegisterHotKey / WM_HOTKEY.</summary>
internal sealed class GlobalHotkeyListener : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xA11C;

    private HotkeyChord? _chord;
    private bool _registered;
    private System.Windows.Forms.Timer? _releasePollTimer;
    private bool _pttHeld;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;

    public bool TryRegister(HotkeyChord chord)
    {
        Unregister();
        _chord = chord;
        if (!RegisterHotKey(Handle, HotkeyId, chord.Modifiers, chord.VirtualKey))
        {
            return false;
        }

        _registered = true;
        return true;
    }

    public void Unregister()
    {
        StopReleasePolling();
        if (_registered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            if (!_pttHeld)
            {
                _pttHeld = true;
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                StartReleasePolling();
            }

            return;
        }

        base.WndProc(ref m);
    }

    private void StartReleasePolling()
    {
        _releasePollTimer?.Stop();
        _releasePollTimer?.Dispose();
        _releasePollTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _releasePollTimer.Tick += (_, _) => PollRelease();
        _releasePollTimer.Start();
    }

    private void StopReleasePolling()
    {
        _releasePollTimer?.Stop();
        _releasePollTimer?.Dispose();
        _releasePollTimer = null;
    }

    private void PollRelease()
    {
        if (_chord is null || !_pttHeld)
        {
            return;
        }

        if (IsChordPhysicallyDown(_chord))
        {
            return;
        }

        _pttHeld = false;
        StopReleasePolling();
        HotkeyReleased?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsChordPhysicallyDown(HotkeyChord chord)
    {
        if (!IsKeyDown(chord.VirtualKey))
        {
            return false;
        }

        if (chord.Ctrl && !IsModifierDown(0x11))
        {
            return false;
        }

        if (chord.Shift && !IsModifierDown(0x10))
        {
            return false;
        }

        if (chord.Alt && !IsModifierDown(0x12))
        {
            return false;
        }

        if (chord.Win && !IsModifierDown(0x5B) && !IsModifierDown(0x5C))
        {
            return false;
        }

        return true;
    }

    private static bool IsKeyDown(uint vk) => (GetAsyncKeyState((int)vk) & 0x8000) != 0;

    private static bool IsModifierDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
#endif
