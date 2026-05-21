using System.Runtime.InteropServices;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Output;

public sealed class ClipboardTranscriptSink : ITranscriptSink
{
    private readonly Func<nint>? _getSettingsWindowHandle;

    public event Action<string, bool>? Emitted;

    public ClipboardTranscriptSink(Func<nint>? getSettingsWindowHandle = null)
    {
        _getSettingsWindowHandle = getSettingsWindowHandle;
    }

    public Task EmitAsync(string textToClipboard, bool pasteToCaret, CancellationToken cancellationToken)
    {
        Emitted?.Invoke(textToClipboard, pasteToCaret);
#if NET8_0_WINDOWS
        SetClipboardWithRetry(textToClipboard);
        if (pasteToCaret)
        {
            TryPasteToCaret();
        }
#else
        Console.WriteLine($"[clipboard-fallback] paste={pasteToCaret} text={textToClipboard}");
#endif
        return Task.CompletedTask;
    }

#if NET8_0_WINDOWS
    private static void SetClipboardWithRetry(string text)
    {
        const int attempts = 3;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                System.Windows.Forms.Clipboard.SetText(text);
                return;
            }
            catch (System.Runtime.InteropServices.ExternalException) when (i < attempts - 1)
            {
                Thread.Sleep(50);
            }
        }
    }

    private void TryPasteToCaret()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return;
        }

        var settingsHandle = _getSettingsWindowHandle?.Invoke() ?? IntPtr.Zero;
        if (settingsHandle != IntPtr.Zero && foreground == settingsHandle)
        {
            return;
        }

        SendCtrlV();
    }

    private static void SendCtrlV()
    {
        var inputs = new INPUT[4];
        inputs[0] = new INPUT { Type = InputKeyboard, Ki = new KEYBDINPUT { Vk = VK_CONTROL } };
        inputs[1] = new INPUT { Type = InputKeyboard, Ki = new KEYBDINPUT { Vk = 0x56 } }; // V
        inputs[2] = new INPUT { Type = InputKeyboard, Ki = new KEYBDINPUT { Vk = 0x56, Flags = KeyeventfKeyup } };
        inputs[3] = new INPUT { Type = InputKeyboard, Ki = new KEYBDINPUT { Vk = VK_CONTROL, Flags = KeyeventfKeyup } };
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int InputKeyboard = 1;
    private const ushort VK_CONTROL = 0x11;
    private const uint KeyeventfKeyup = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int Type;
        public KEYBDINPUT Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
#endif
}
