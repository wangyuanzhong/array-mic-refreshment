#if WINDOWS

using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Global PTT via WH_KEYBOARD_LL — works when <see cref="RegisterHotKey"/> WM_HOTKEY never reaches tray apps.
/// Press/release handlers and release polling are marshaled to a UI <see cref="Control"/> (never block the hook).
/// </summary>
public sealed class LowLevelHotkeyHost : IGlobalHotkeyHost
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSysKeydown = 0x0104;
    private const int WmSysKeyup = 0x0105;
    private const uint LlkhfAltdown = 0x20;
    private const uint LlkhfRepeat = 0x4000;

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private HotkeyChord? _chord;
    private bool _pttHeld;
    private int _releaseStreak;
    private DateTimeOffset _releaseCooldownUtc;
    private System.Windows.Forms.Timer? _releasePollTimer;
    private bool _ctrlDown;
    private bool _altDown;
    private bool _shiftDown;
    private bool _winDown;
    private Control? _uiAnchor;

    public LowLevelHotkeyHost()
    {
        _hookProc = OnHook;
    }

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;
    public event Action<IntPtr>? ForegroundAtPress;
    public event Action<IntPtr>? ForegroundAtRelease;

    public bool IsRegistered => _chord is not null && _hookHandle != IntPtr.Zero;

    /// <summary>WinForms anchor for timer + event delivery (tray message pump).</summary>
    public void BindUiAnchor(Control anchor) => _uiAnchor = anchor;

    public bool TryRegister(HotkeyChord chord)
    {
        if (!EnsureHookInstalled())
        {
            return false;
        }

        _chord = chord;
        Log.Information("PTT low-level keyboard hook active ({Display})", chord);
        return true;
    }

    public void Unregister()
    {
        MarshalToUi(StopReleasePolling);
        _chord = null;
        _pttHeld = false;
        _releaseStreak = 0;
        ResetModifierState();
        UninstallHook();
    }

    public void ResetHeldState()
    {
        _pttHeld = false;
        _releaseStreak = 0;
        MarshalToUi(StopReleasePolling);
    }

    public void Dispose() => Unregister();

    private bool EnsureHookInstalled()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        var moduleHandle = GetModuleHandleForHook();
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            Log.Warning("PTT SetWindowsHookEx(WH_KEYBOARD_LL) failed (win32={Err})", err);
            return false;
        }

        return true;
    }

    private static IntPtr GetModuleHandleForHook()
    {
        using var process = Process.GetCurrentProcess();
        var moduleName = process.MainModule?.ModuleName;
        if (!string.IsNullOrEmpty(moduleName))
        {
            var fromModule = GetModuleHandle(moduleName);
            if (fromModule != IntPtr.Zero)
            {
                return fromModule;
            }
        }

        return GetModuleHandle(null);
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr OnHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _chord is not null)
        {
            var msg = wParam.ToInt32();
            var keyDown = msg is WmKeydown or WmSysKeydown;
            var keyUp = msg is WmKeyup or WmSysKeyup;
            if (keyDown || keyUp)
            {
                var data = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
                UpdateModifierState(data.VirtualKey, keyDown);
                if (keyDown)
                {
                    TryHandleKeyDown(data);
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void UpdateModifierState(uint vk, bool down)
    {
        switch (vk)
        {
            case 0x11 or 0xA2 or 0xA3:
                _ctrlDown = down;
                break;
            case 0x12 or 0xA4 or 0xA5:
                _altDown = down;
                break;
            case 0x10 or 0xA0 or 0xA1:
                _shiftDown = down;
                break;
            case 0x5B or 0x5C:
                _winDown = down;
                break;
        }
    }

    private void ResetModifierState()
    {
        _ctrlDown = false;
        _altDown = false;
        _shiftDown = false;
        _winDown = false;
    }

    private void TryHandleKeyDown(KbdllHookStruct data)
    {
        if (_chord is null || _pttHeld || DateTimeOffset.UtcNow < _releaseCooldownUtc)
        {
            return;
        }

        if (data.VirtualKey != _chord.VirtualKey)
        {
            return;
        }

        if ((data.Flags & LlkhfRepeat) != 0)
        {
            return;
        }

        // Modifier state from the hook thread is unreliable (keys held before hook install,
        // GetAsyncKeyState inside WH_KEYBOARD_LL). Verify on the UI thread instead.
        var fg = GetForegroundWindow();
        var chord = _chord;
        var vk = data.VirtualKey;
        MarshalToUi(() => TryChordDownOnUi(chord, vk, fg));
    }

    private void TryChordDownOnUi(HotkeyChord chord, uint vk, IntPtr fg)
    {
        if (_chord is null || _pttHeld || DateTimeOffset.UtcNow < _releaseCooldownUtc)
        {
            return;
        }

        if (vk != chord.VirtualKey)
        {
            return;
        }

        if (!ModifiersMatch(chord))
        {
            Log.Debug(
                "PTT main key 0x{Vk:X} ignored on UI thread — modifiers mismatch (want {Chord}, ctrl={Ctrl} alt={Alt} shift={Shift} win={Win})",
                vk,
                chord,
                IsCtrlDown(),
                IsAltDown(),
                IsShiftDown(),
                IsWinDown());
            return;
        }

        _pttHeld = true;
        _releaseStreak = 0;
        OnChordDownOnUi(chord, fg);
    }

    private void OnChordDownOnUi(HotkeyChord chord, IntPtr fg)
    {
        Log.Information("PTT hotkey chord down via keyboard hook ({Chord})", chord);
        ForegroundAtPress?.Invoke(fg);
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
        StartReleasePolling();
    }

    private static bool ModifiersMatch(HotkeyChord chord) =>
        chord.Ctrl == IsCtrlDown()
        && chord.Alt == IsAltDown()
        && chord.Shift == IsShiftDown()
        && chord.Win == IsWinDown();

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

        // PTT ends when the main key is released (modifiers may still be held for chord combos).
        if (IsKeyDown((int)_chord.VirtualKey))
        {
            _releaseStreak = 0;
            return;
        }

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
        Log.Information("PTT hotkey chord released via keyboard hook");
        ForegroundAtRelease?.Invoke(GetForegroundWindow());
        HotkeyReleased?.Invoke(this, EventArgs.Empty);
    }

    private void MarshalToUi(Action action)
    {
        if (_uiAnchor is { IsHandleCreated: true } && _uiAnchor.InvokeRequired)
        {
            _uiAnchor.BeginInvoke(action);
            return;
        }

        action();
    }

    private static bool IsCtrlDown() =>
        IsKeyDown(0x11) || IsKeyDown(0xA2) || IsKeyDown(0xA3);

    private static bool IsAltDown() =>
        IsKeyDown(0x12) || IsKeyDown(0xA4) || IsKeyDown(0xA5);

    private static bool IsShiftDown() =>
        IsKeyDown(0x10) || IsKeyDown(0xA0) || IsKeyDown(0xA1);

    private static bool IsWinDown() =>
        IsKeyDown(0x5B) || IsKeyDown(0x5C);

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdllHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}

#endif
