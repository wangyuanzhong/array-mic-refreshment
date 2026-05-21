using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

public sealed class SettingsForm : Form
{
    private readonly CheckBox _refineEnabled = new() { Text = "启用提示词整理（默认建议关闭）", AutoSize = true };
    private readonly TextBox _apiUrl = new() { Width = 360 };
    private readonly TextBox _apiKey = new() { Width = 360, UseSystemPasswordChar = true };
    private readonly TextBox _apiModel = new() { Width = 360 };
    private readonly TextBox _skillsDir = new() { Width = 360 };
    private readonly TextBox _pttHotkey = new() { Width = 200 };
    private readonly ComboBox _forcedIntent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _onRefineFailure = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Settings = settings;
        Text = "Array Mic Refreshment — 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 380);

        foreach (PromptIntent value in Enum.GetValues<PromptIntent>())
        {
            _forcedIntent.Items.Add(value);
        }

        foreach (OnRefineFailure value in Enum.GetValues<OnRefineFailure>())
        {
            _onRefineFailure.Items.Add(value);
        }

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddRow(int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        AddRow(0, "API Base URL", _apiUrl);
        AddRow(1, "API Key", _apiKey);
        AddRow(2, "Model", _apiModel);
        AddRow(3, "", _refineEnabled);
        layout.SetColumnSpan(_refineEnabled, 2);
        AddRow(4, "Skills 目录", _skillsDir);
        AddRow(5, "PTT 热键", _pttHotkey);
        AddRow(6, "强制意图", _forcedIntent);
        AddRow(7, "整理失败时", _onRefineFailure);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => ApplyToSettings();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _apiUrl.Text = Settings.ApiBaseUrl;
        _apiKey.Text = Settings.ApiKey;
        _apiModel.Text = Settings.ApiModel;
        _refineEnabled.Checked = Settings.PromptRefineEnabled;
        _skillsDir.Text = Settings.SkillsDirectory;
        _pttHotkey.Text = Settings.PttHotkey;
        _forcedIntent.SelectedItem = Settings.ForcedIntent;
        _onRefineFailure.SelectedItem = Settings.OnRefineFailure;
    }

    private void ApplyToSettings()
    {
        Settings = new AppSettings
        {
            MasterEnabled = Settings.MasterEnabled,
            PasteToCaretEnabled = Settings.PasteToCaretEnabled,
            PromptRefineEnabled = _refineEnabled.Checked,
            ForcedIntent = (PromptIntent)(_forcedIntent.SelectedItem ?? PromptIntent.Auto),
            OnRefineFailure = (OnRefineFailure)(_onRefineFailure.SelectedItem ?? OnRefineFailure.UseRawTranscript),
            SelectedDeviceId = Settings.SelectedDeviceId,
            CurrentSpeakerUserId = Settings.CurrentSpeakerUserId,
            PttHotkey = _pttHotkey.Text.Trim(),
            ApiBaseUrl = _apiUrl.Text.Trim(),
            ApiKey = _apiKey.Text,
            ApiModel = _apiModel.Text.Trim(),
            SkillsDirectory = _skillsDir.Text.Trim(),
            ModelsDirectory = Settings.ModelsDirectory,
        };
    }
}
