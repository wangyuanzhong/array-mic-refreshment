using ArrayMicRefreshment.App.Controls;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Modal dialog for capturing a PTT hotkey via <see cref="HotkeyCaptureTextBox"/>.</summary>
public sealed class HotkeyCaptureDialog : Form
{
    private readonly HotkeyCaptureTextBox _hotkeyBox = new()
    {
        Dock = DockStyle.Top,
        Height = 36,
    };

    private readonly Button _okButton = new() { Text = "确定", DialogResult = DialogResult.OK, Width = 96, Height = 32 };
    private readonly Button _cancelButton = new() { Text = "取消", DialogResult = DialogResult.Cancel, Width = 96, Height = 32 };

    public HotkeyCaptureDialog(string currentHotkey, IWin32Window? owner = null)
    {
        Text = "录入 PTT 热键";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        ClientSize = new Size(520, 200);
        MinimumSize = new Size(480, 180);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        KeyPreview = true;

        _hotkeyBox.HotkeyExpression = currentHotkey ?? string.Empty;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "点击输入框后按下组合键（勿用键盘输入文字）",
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(480, 0),
            ForeColor = SystemColors.GrayText,
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "建议：Ctrl+Alt+字母/空格；需包含 Ctrl 或 Alt，避免单独功能键。",
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(480, 0),
            ForeColor = SystemColors.GrayText,
        }, 0, 1);

        layout.Controls.Add(_hotkeyBox, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(_cancelButton);
        buttons.Controls.Add(_okButton);
        layout.Controls.Add(buttons, 0, 3);

        Controls.Add(layout);

        _hotkeyBox.Font = new Font(Font.FontFamily, Font.Size + 1f, FontStyle.Regular);

        Shown += (_, _) =>
        {
            Activate();
            _hotkeyBox.Focus();
            _hotkeyBox.BeginCapturePublic();
        };

        KeyDown += OnDialogKeyDown;
        _hotkeyBox.HotkeyCaptured += (_, expression) =>
        {
            _hotkeyBox.HotkeyExpression = expression;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    public string CapturedHotkey => _hotkeyBox.HotkeyExpression;

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_hotkeyBox.CaptureFocus)
        {
            _hotkeyBox.Focus();
            _hotkeyBox.BeginCapturePublic();
        }

        _hotkeyBox.ProcessKeyDown(e);
    }

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
