#if WINDOWS

namespace ArrayMicRefreshment.Audio;



/// <summary>Global PTT: RegisterHotKey on press, release when the main key comes up.</summary>

internal sealed class GlobalHotkeyListener : NativeWindow, IDisposable

{

    private const int WmHotkey = 0x0312;

    private const int HotkeyId = 0xA11C;

    private const uint ModNorepeat = 0x4000;



    private HotkeyChord? _chord;

    private bool _pttHeld;

    private int _releaseStreak;

    private DateTimeOffset _releaseCooldownUtc;

    private System.Windows.Forms.Timer? _releasePollTimer;



    public event EventHandler? HotkeyPressed;

    public event EventHandler? HotkeyReleased;

    public event Action<IntPtr>? ForegroundAtPress;

    public event Action<IntPtr>? ForegroundAtRelease;



    public GlobalHotkeyListener()

    {

        CreateHandle(new CreateParams());

    }



    public bool TryRegister(HotkeyChord chord)

    {

        Unregister();

        _chord = chord;

        var mods = chord.Modifiers | ModNorepeat;

        if (!RegisterHotKey(Handle, HotkeyId, mods, chord.VirtualKey))

        {

            _chord = null;

            return false;

        }



        return true;

    }



    internal void ResetHeldState()

    {

        _pttHeld = false;

        _releaseStreak = 0;

        StopReleasePolling();

    }



    public void Unregister()

    {

        StopReleasePolling();

        if (_chord is not null)

        {

            UnregisterHotKey(Handle, HotkeyId);

            _chord = null;

        }



        _pttHeld = false;

        _releaseStreak = 0;

    }



    protected override void WndProc(ref Message m)

    {

        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)

        {

            if (!_pttHeld && DateTimeOffset.UtcNow >= _releaseCooldownUtc)

            {

                _pttHeld = true;

                _releaseStreak = 0;

                ForegroundAtPress?.Invoke(GetForegroundWindow());

                HotkeyPressed?.Invoke(this, EventArgs.Empty);

                StartReleasePolling();

            }



            return;

        }



        base.WndProc(ref m);

    }



    private void StartReleasePolling()

    {

        StopReleasePolling();

        _releasePollTimer = new System.Windows.Forms.Timer { Interval = 25 };

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
            StopReleasePolling();
            return;
        }

        // Wait until BOTH the main key AND all modifiers are released.
        // If we fire HotkeyReleased while Ctrl/Shift/Alt/Win are still held,
        // the subsequent Ctrl+V paste will be swallowed or misinterpreted.
        if (IsKeyDown((int)_chord.VirtualKey))
        {
            _releaseStreak = 0;
            return;
        }

        if (_chord.Ctrl  && IsKeyDown(0x11)) { _releaseStreak = 0; return; } // VK_CONTROL
        if (_chord.Alt   && IsKeyDown(0x12)) { _releaseStreak = 0; return; } // VK_MENU
        if (_chord.Shift && IsKeyDown(0x10)) { _releaseStreak = 0; return; } // VK_SHIFT
        if (_chord.Win   && IsKeyDown(0x5B)) { _releaseStreak = 0; return; } // VK_LWIN

        _releaseStreak++;
        if (_releaseStreak < 2)
        {
            return;
        }

        CommitRelease();
    }

    private void CommitRelease()
    {
        if (!_pttHeld)
        {
            return;
        }

        _pttHeld = false;
        _releaseStreak = 0;
        _releaseCooldownUtc = DateTimeOffset.UtcNow.AddMilliseconds(200);
        StopReleasePolling();
        ForegroundAtRelease?.Invoke(GetForegroundWindow());
        HotkeyReleased?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;



    public void Dispose()

    {

        Unregister();

        DestroyHandle();

    }



    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]

    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);



    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]

    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);



    [System.Runtime.InteropServices.DllImport("user32.dll")]

    private static extern IntPtr GetForegroundWindow();



    [System.Runtime.InteropServices.DllImport("user32.dll")]

    private static extern short GetAsyncKeyState(int vKey);

}

#endif

