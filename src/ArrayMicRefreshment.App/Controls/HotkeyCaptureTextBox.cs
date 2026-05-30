using System.Runtime.InteropServices;
using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.App.Controls;

/// <summary>
/// Read-only text box: click and press a key combination to capture PTT hotkey (not typed as text).
/// </summary>
public sealed class HotkeyCaptureTextBox : TextBox
{
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private bool _capturing;
    private string _committed = string.Empty;

    public event EventHandler<string>? HotkeyCaptured;

    /// <summary>True while waiting for the user to press a chord.</summary>
    public bool CaptureFocus => _capturing;

    public HotkeyCaptureTextBox()
    {
        ReadOnly = true;
        Cursor = Cursors.Hand;
    }

    public string HotkeyExpression
    {
        get => string.IsNullOrWhiteSpace(Text) || _capturing ? _committed : Text.Trim();
        set
        {
            _committed = value ?? string.Empty;
            Text = _committed;
            _capturing = false;
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        BeginCapture();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (!_capturing)
        {
            BeginCapture();
        }
    }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        if (_capturing)
        {
            e.IsInputKey = true;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_capturing)
        {
            base.OnKeyDown(e);
            return;
        }

        ProcessKeyDown(e);
    }

    public void BeginCapturePublic() => BeginCapture();

    public void ProcessKeyDown(KeyEventArgs e)
    {
        if (!_capturing)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        if (IsModifierKey(e.KeyCode))
        {
            Text = FormatModifiers(e) + "...";
            return;
        }

        if (!TryBuildFromKeyEvent(e, out var expression, out var error))
        {
            Text = error ?? "无效热键";
            return;
        }

        CommitCapture(expression);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_capturing)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyUp(e);
    }

    protected override void OnLeave(EventArgs e)
    {
        if (_capturing)
        {
            _capturing = false;
            Text = _committed;
            BackColor = SystemColors.Window;
        }

        base.OnLeave(e);
    }

    private void BeginCapture()
    {
        _capturing = true;
        Text = "请按下热键组合...";
        BackColor = Color.FromArgb(255, 255, 224);
        SelectAll();
    }

    private void CommitCapture(string expression)
    {
        _committed = expression;
        Text = expression;
        _capturing = false;
        BackColor = SystemColors.Window;
        HotkeyCaptured?.Invoke(this, expression);
    }

    private static bool IsModifierKey(Keys key) =>
        key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin;

    private static bool TryBuildFromKeyEvent(KeyEventArgs e, out string expression, out string? error)
    {
        if (IsModifierKey(e.KeyCode))
        {
            expression = string.Empty;
            error = "请同时按下主键";
            return false;
        }

        var vk = KeyCodeToVirtualKey(e.KeyCode);
        var win = IsWindowsKeyPhysicallyDown();
        return HotkeyCapture.TryBuildExpression(e.Control, e.Shift, e.Alt, win, vk, out expression, out error);
    }

    private static uint KeyCodeToVirtualKey(Keys key)
    {
        var code = (int)key & 0xFFFF;
        if (code is >= (int)Keys.F1 and <= (int)Keys.F24)
        {
            return 0x6F + (uint)(code - (int)Keys.F1 + 1);
        }

        return (uint)code;
    }

    private static string FormatModifiers(KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.Control)
        {
            parts.Add("Ctrl");
        }

        if (e.Shift)
        {
            parts.Add("Shift");
        }

        if (e.Alt)
        {
            parts.Add("Alt");
        }

        if (IsWindowsKeyPhysicallyDown())
        {
            parts.Add("Win");
        }

        return parts.Count == 0 ? "" : string.Join('+', parts) + "+";
    }

    private static bool IsWindowsKeyPhysicallyDown() =>
        IsKeyDown(VkLWin) || IsKeyDown(VkRWin);

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
