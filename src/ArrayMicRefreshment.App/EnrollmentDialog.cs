using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App;

public sealed class EnrollmentDialog : Form
{
    private static readonly string[] Phrases =
    [
        "请说：今天天气很好",
        "请说：打开代码编辑器",
        "请说：我要写一封邮件",
    ];

    private readonly IUserEnrollmentService _enrollment;
    private readonly IEnrollmentUtteranceSource? _capture;
    private readonly TextBox _nameBox = new() { Width = 280 };
    private readonly Label _phraseLabel = new() { AutoSize = true, MaximumSize = new Size(360, 0) };
    private readonly Label _progressLabel = new() { AutoSize = true };
    private readonly Button _recordButton = new() { Text = "开始录音", Width = 100 };
    private readonly Button _finishButton = new() { Text = "完成注册", Width = 100, Enabled = false };
    private readonly List<AudioUtterance> _utterances = new();
    private EnrollmentRecordingSession? _session;
    private int _phraseIndex;

    public EnrollmentDialog(IUserEnrollmentService enrollment, IEnrollmentUtteranceSource? capture)
    {
        _enrollment = enrollment;
        _capture = capture;
        Text = "注册说话人";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 220);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "姓名", AutoSize = true }, 0, 0);
        layout.Controls.Add(_nameBox, 1, 0);
        layout.Controls.Add(_phraseLabel, 0, 1);
        layout.SetColumnSpan(_phraseLabel, 2);
        layout.Controls.Add(_progressLabel, 0, 2);
        layout.SetColumnSpan(_progressLabel, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_recordButton);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);

        layout.Controls.Add(_finishButton, 1, 4);

        _recordButton.Click += OnRecordClick;
        _finishButton.Click += OnFinishClick;

        Controls.Add(layout);
        UpdatePhraseUi();
    }

    private void UpdatePhraseUi()
    {
        if (_phraseIndex >= Phrases.Length)
        {
            _phraseLabel.Text = "全部完成，可点击「完成注册」。";
            _progressLabel.Text = string.Join(" ", Enumerable.Range(0, Phrases.Length).Select(i => "✓"));
            _recordButton.Enabled = false;
            _finishButton.Enabled = _utterances.Count == Phrases.Length && !string.IsNullOrWhiteSpace(_nameBox.Text);
            return;
        }

        _phraseLabel.Text = $"第 {_phraseIndex + 1}/{Phrases.Length} 段：{Phrases[_phraseIndex]}";
        var marks = string.Concat(Enumerable.Range(0, Phrases.Length).Select(i =>
            i < _utterances.Count ? "✓ " : i == _phraseIndex ? "● " : "○ "));
        _progressLabel.Text = marks.Trim();
        _recordButton.Text = _session is null ? "开始录音" : "停止";
        _finishButton.Enabled = false;
    }

    private void OnRecordClick(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            if (_capture is null)
            {
                MessageBox.Show(
                    this,
                    "未配置录音采集源。请在 Windows 下通过托盘打开注册。",
                    "无法录音",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _session = _capture.StartRecording();
                _recordButton.Text = "停止";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "录音失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return;
        }

        var utterance = _session.Stop();
        _session.Dispose();
        _session = null;
        _recordButton.Text = "开始录音";

        if (utterance is null || utterance.Duration < TimeSpan.FromSeconds(1))
        {
            MessageBox.Show(this, "录音太短，请录 3~5 秒。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (utterance.Duration > TimeSpan.FromSeconds(8))
        {
            MessageBox.Show(this, "录音较长，已接受本段。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        _utterances.Add(utterance);
        _phraseIndex++;
        UpdatePhraseUi();
    }

    private void OnFinishClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "请输入姓名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_utterances.Count < Phrases.Length)
        {
            MessageBox.Show(this, "请完成全部 3 段录音。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _enrollment.AddUser(_nameBox.Text.Trim(), _utterances);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "注册失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _session?.Dispose();
        base.OnFormClosing(e);
    }
}
