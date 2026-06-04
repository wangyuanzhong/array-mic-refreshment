#if WINDOWS

using System.Runtime.InteropServices;

namespace ArrayMicRefreshment.Audio;

internal static class NativeKeyboardInput
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    public static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    /// <summary>Inject key-up so a swallowed PTT main key does not stay logically pressed.</summary>
    public static void SendKeyUp(ushort virtualKey)
    {
        if (!IsKeyDown(virtualKey))
        {
            return;
        }

        var input = new INPUT
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = virtualKey,
                    Flags = KeyeventfKeyup,
                },
            },
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
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
}

#endif
