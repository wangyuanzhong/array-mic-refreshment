#if WINDOWS

using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>Global PTT: RegisterHotKey on a hidden Form (reliable WM_HOTKEY in tray apps).</summary>
public sealed class GlobalHotkeyListener : Form, IGlobalHotkeyHost
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xA11C;
    private const uint ModNorepeat = 0x4000;

    private HotkeyChord? _chord;
    private bool _pttHeld;
    private int _releaseStreak;
    private DateTimeOffset _releaseCooldownUtc;
    private DateTimeOffset _pressUtc;
    private System.Windows.Forms.Timer? _releasePollTimer;

    public GlobalHotkeyListener()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
    }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;
    public event Action<IntPtr>? ForegroundAtPress;
    public event Action<IntPtr>? ForegroundAtRelease;

    public PttRecordingMode RecordingMode { get; set; } = PttRecordingMode.Hold;

    public bool IsRegistered => _chord is not null;

    public bool TryRegister(HotkeyChord chord)
    {
        if (!IsHandleCreated)
        {
            CreateHandle();
        }

        Unregister();
        _chord = chord;
        var mods = chord.Modifiers | ModNorepeat;
        if (!RegisterHotKey(Handle, HotkeyId, mods, chord.VirtualKey))
        {
            var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Log.Warning(
                "PTT RegisterHotKey failed (hwnd=0x{Hwnd:X}, {Chord}, win32={Err})",
                Handle.ToInt64(),
                chord,
                err);
            _chord = null;
            return false;
        }

        Log.Information(
            "PTT RegisterHotKey ok (hwnd=0x{Hwnd:X}, {Chord})",
            Handle.ToInt64(),
            chord);
        return true;
    }

    public void ResetHeldState()
    {
        _pttHeld = false;
        _releaseStreak = 0;
        StopReleasePolling();
    }

    public void Unregister()
    {
        StopReleasePolling();
        if (_chord is not null && IsHandleCreated)
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
            if (DateTimeOffset.UtcNow < _releaseCooldownUtc)
            {
                return;
            }

            var fg = GetForegroundWindow();
            PttHotkeyInteraction.OnHotkeyActivation(
                RecordingMode,
                ref _pttHeld,
                () =>
                {
                    _releaseStreak = 0;
                    _pressUtc = DateTimeOffset.UtcNow;
                    Log.Information(
                        "PTT hotkey down ({Mode}, {Chord})",
                        RecordingMode,
                        _chord);
                    ForegroundAtPress?.Invoke(fg);
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    StartReleasePolling();
                },
                () =>
                {
                    _releaseStreak = 0;
                    _releaseCooldownUtc = DateTimeOffset.UtcNow.AddMilliseconds(200);
                    StopReleasePolling();
                    Log.Information(
                        "PTT hotkey toggle stop ({Chord})",
                        _chord);
                    ForegroundAtRelease?.Invoke(GetForegroundWindow());
                    HotkeyReleased?.Invoke(this, EventArgs.Empty);
                });

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

        if (RecordingMode == PttRecordingMode.Toggle)
        {
            if (DateTimeOffset.UtcNow - _pressUtc > TimeSpan.FromMinutes(10))
            {
                Log.Warning("PTT toggle auto-stopped after 10 minutes");
                CommitRelease();
            }

            return;
        }

        if (IsChordPhysicallyHeld(_chord))
        {
            _releaseStreak = 0;
            if (DateTimeOffset.UtcNow - _pressUtc > TimeSpan.FromMinutes(10))
            {
                Log.Warning("PTT auto-released after 10 minutes (stuck key state?)");
                CommitRelease();
            }

            return;
        }

        _releaseStreak++;
        if (_releaseStreak < 2)
        {
            return;
        }

        CommitRelease();
    }

    private static bool IsChordPhysicallyHeld(HotkeyChord chord)
    {
        if (IsKeyDown((int)chord.VirtualKey))
        {
            return true;
        }

        if (chord.Ctrl && !IsCtrlDown())
        {
            return false;
        }

        if (chord.Alt && !IsAltDown())
        {
            return false;
        }

        if (chord.Shift && !IsShiftDown())
        {
            return false;
        }

        if (chord.Win && !IsWinDown())
        {
            return false;
        }

        return false;
    }

    private static bool IsCtrlDown() =>
        IsKeyDown(0x11) || IsKeyDown(0xA2) || IsKeyDown(0xA3);

    private static bool IsAltDown() =>
        IsKeyDown(0x12) || IsKeyDown(0xA4) || IsKeyDown(0xA5);

    private static bool IsShiftDown() =>
        IsKeyDown(0x10) || IsKeyDown(0xA0) || IsKeyDown(0xA1);

    private static bool IsWinDown() =>
        IsKeyDown(0x5B) || IsKeyDown(0x5C);

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
        Log.Information("PTT hotkey chord released via RegisterHotKey");
        ForegroundAtRelease?.Invoke(GetForegroundWindow());
        HotkeyReleased?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Unregister();
        }

        base.Dispose(disposing);
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
