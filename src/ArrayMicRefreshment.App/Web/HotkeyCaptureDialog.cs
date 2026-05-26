using ArrayMicRefreshment.App.Controls;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Modal dialog for capturing a PTT hotkey via <see cref="HotkeyCaptureTextBox"/>.</summary>
public sealed class HotkeyCaptureDialog : Form
{
    private readonly HotkeyCaptureTextBox _hotkeyBox = new() { Width = 280, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _okButton = new() { Text = "确定", DialogResult = DialogResult.OK, Width = 80 };
    private readonly Button _cancelButton = new() { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };

    public HotkeyCaptureDialog(string currentHotkey, IWin32Window? owner = null)
    {
        Text = "录入 PTT 热键";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        ClientSize = new Size(360, 120);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        _hotkeyBox.HotkeyExpression = currentHotkey ?? string.Empty;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "点击输入框后按下组合键（勿用键盘输入文字）",
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            ForeColor = SystemColors.GrayText,
        }, 0, 0);
        layout.Controls.Add(_hotkeyBox, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };
        buttons.Controls.Add(_cancelButton);
        buttons.Controls.Add(_okButton);
        layout.Controls.Add(buttons, 0, 2);

        Controls.Add(layout);
    }

    public string CapturedHotkey => _hotkeyBox.HotkeyExpression;

    public static HotkeyCaptureResultDto ShowDialog(IWin32Window? owner, string currentHotkey)
    {
        using var dialog = new HotkeyCaptureDialog(currentHotkey, owner);
        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return new HotkeyCaptureResultDto
        {
            Hotkey = dialog.CapturedHotkey,
            Cancelled = result != DialogResult.OK,
        };
    }
}
