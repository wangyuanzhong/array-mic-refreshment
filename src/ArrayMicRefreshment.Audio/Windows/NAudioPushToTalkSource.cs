#if WINDOWS

using ArrayMicRefreshment.Core;



namespace ArrayMicRefreshment.Audio;



/// <summary>Global PTT via RegisterHotKey; release detected by UI-thread key polling.</summary>

public sealed class NAudioPushToTalkSource : IPushToTalkSource, IDisposable

{

    private readonly GlobalHotkeyListener _listener = new();

    private HotkeyChord? _chord;

    private int _releaseCount;



    public NAudioPushToTalkSource(string hotkeyExpression)

    {

        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out _))

        {

            chord = new HotkeyChord { Ctrl = true, Alt = true, VirtualKey = 0x20 };

        }



        _chord = chord;

        HotkeyDisplay = chord!.ToString();

        _listener.ForegroundAtPress += hwnd => ForegroundAtPress?.Invoke(hwnd);
        _listener.ForegroundAtRelease += hwnd => ForegroundAtRelease?.Invoke(hwnd);
        _listener.HotkeyPressed += (_, _) => PttPressed?.Invoke(this, EventArgs.Empty);

        _listener.HotkeyReleased += (_, _) =>

        {

            _releaseCount++;

            PttReleased?.Invoke(this, EventArgs.Empty);

        };

        IsRegistered = _listener.TryRegister(chord!);

    }



    public event EventHandler? PttPressed;

    public event EventHandler? PttReleased;

    public event Action<IntPtr>? ForegroundAtPress;

    public event Action<IntPtr>? ForegroundAtRelease;

    public string HotkeyDisplay { get; private set; }



    public bool IsRegistered { get; }



    public int ReleaseEventCount => _releaseCount;



    public bool TryUpdateHotkey(string hotkeyExpression, out string? error)

    {

        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out error))

        {

            return false;

        }



        _listener.Unregister();

        if (!_listener.TryRegister(chord!))

        {

            error = "热键无法注册（可能已被其它程序占用）。请换一个组合。";

            if (_chord is not null)

            {

                _listener.TryRegister(_chord);

            }



            return false;

        }



        _chord = chord;

        HotkeyDisplay = chord!.ToString();

        return true;

    }



    public void UpdateHotkey(string hotkeyExpression) => TryUpdateHotkey(hotkeyExpression, out _);



    public void SimulatePress() => PttPressed?.Invoke(this, EventArgs.Empty);



    public void SimulateRelease()

    {

        _listener.ResetHeldState();

        _releaseCount++;

        PttReleased?.Invoke(this, EventArgs.Empty);

    }



    public void Dispose() => _listener.Dispose();

}

#endif

