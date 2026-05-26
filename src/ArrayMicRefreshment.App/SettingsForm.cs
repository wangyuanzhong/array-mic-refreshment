using ArrayMicRefreshment.App.Controls;
using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App;

/// <summary>Display wrapper for a PromptIntent skill choice.</summary>
public sealed class SkillOption
{
    public string Label { get; }
    public PromptIntent Value { get; }

    public SkillOption(string label, PromptIntent value)
    {
        Label = label;
        Value = value;
    }

    public override string ToString() => Label;
}

public sealed class TriggerModeOption
{
    public string Label { get; }
    public VoiceTriggerMode Value { get; }

    public TriggerModeOption(string label, VoiceTriggerMode value)
    {
        Label = label;
        Value = value;
    }

    public override string ToString() => Label;
}

public static class TriggerModeOptions
{
    public static readonly IReadOnlyList<TriggerModeOption> All = new List<TriggerModeOption>
    {
        new("PTT（按住热键）", VoiceTriggerMode.PttOnly),
        new("唤醒词", VoiceTriggerMode.WakeWordOnly),
        new("PTT + 唤醒词", VoiceTriggerMode.Both),
    };
}

public sealed class WakeWordSensitivityOption
{
    public string Label { get; }
    public WakeWordSensitivity Value { get; }

    public WakeWordSensitivityOption(string label, WakeWordSensitivity value)
    {
        Label = label;
        Value = value;
    }

    public override string ToString() => Label;
}

public static class WakeWordSensitivityOptions
{
    public static readonly IReadOnlyList<WakeWordSensitivityOption> All = new List<WakeWordSensitivityOption>
    {
        new("标准", WakeWordSensitivity.Standard),
        new("高", WakeWordSensitivity.High),
        new("最高（默认，小声/远距）", WakeWordSensitivity.Maximum),
    };
}

public sealed class HudCornerOption
{
    public string Label { get; }
    public HudScreenCorner Value { get; }

    public HudCornerOption(string label, HudScreenCorner value)
    {
        Label = label;
        Value = value;
    }

    public override string ToString() => Label;
}

public static class HudCornerOptions
{
    public static readonly IReadOnlyList<HudCornerOption> All = new List<HudCornerOption>
    {
        new("右下角", HudScreenCorner.BottomRight),
        new("左下角", HudScreenCorner.BottomLeft),
        new("右上角", HudScreenCorner.TopRight),
        new("左上角", HudScreenCorner.TopLeft),
    };
}

public static class SkillOptions
{
    public static readonly IReadOnlyList<SkillOption> All = new List<SkillOption>
    {
        new("自动判断（让 AI 选择）", PromptIntent.Auto),
        new("纯文本整理 — 去口误、加标点", PromptIntent.PlainText),
        new("通用 AI Prompt", PromptIntent.GeneralAi),
        new("代码编辑指令", PromptIntent.CodeEditing),
        new("深度研究 Prompt", PromptIntent.Research),
        new("待办列表", PromptIntent.TaskPlan),
    };
}

public sealed class SettingsForm : Form
{
    private readonly IAudioDeviceEnumerator? _deviceEnumerator;
    private readonly IUserEnrollmentService? _enrollment;
    private readonly IEnrollmentUtteranceSource? _enrollmentCapture;
    private readonly bool _speakerModelMissing;
    private IReadOnlyList<DeviceComboPopulator.DeviceListItem> _deviceItems = Array.Empty<DeviceComboPopulator.DeviceListItem>();

    /// <summary>Tracks the preset index BEFORE the current change so we can save to the correct slot.</summary>
    private int _previousPresetIndex;

    /// <summary>Suppresses <see cref="OnLlmPresetChanged"/> while combo items are updated programmatically.</summary>
    private bool _suppressPresetComboEvents;

    private bool _testConnectionRunning;

    private readonly ComboBox _deviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ComboBox _currentUserCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly Button _addUserButton = new() { Text = "新增用户…", AutoSize = true };
    private readonly Button _deleteUserButton = new() { Text = "删除", AutoSize = true, Width = 72 };
    private readonly NumericUpDown _speakerThreshold = new()
    {
        Minimum = 0.25m,
        Maximum = 0.85m,
        Increment = 0.05m,
        DecimalPlaces = 2,
        Width = 80,
    };
    private readonly Label _speakerStatus = new()
    {
        AutoSize = true,
        MaximumSize = new Size(360, 0),
        ForeColor = SystemColors.GrayText,
    };
    private readonly ComboBox _asrModelCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Button _downloadModelButton = new() { Text = "下载", AutoSize = true };
    private readonly ProgressBar _downloadProgress = new() { Width = 120, Height = 16, Visible = false };
    private readonly Label _asrModelStatus = new()
    {
        AutoSize = true,
        MaximumSize = new Size(360, 0),
        ForeColor = SystemColors.GrayText,
    };
    private readonly CheckBox _refineEnabled = new() { Text = "启用提示词整理（默认建议关闭）", AutoSize = true };
    private readonly CheckBox _launchAtStartup = new() { Text = "", AutoSize = true, Checked = true };
    private readonly ComboBox _llmPresetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _llmPresetName = new() { Width = 360 };
    private readonly TextBox _apiUrl = new() { Width = 360 };
    private readonly TextBox _apiKey = new() { Width = 360, UseSystemPasswordChar = true };
    private readonly TextBox _apiModel = new() { Width = 360 };
    private readonly TextBox _skillsDir = new() { Width = 360 };
    private readonly ComboBox _triggerMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private VoiceTriggerMode _loadedTriggerMode = VoiceTriggerMode.PttOnly;
    private readonly TextBox _wakeWordPhrase = new() { Width = 360 };
    private TableLayoutPanel? _settingsLayout;
    private const int WakeWordSectionRow = 15;
    private Control? _wakeWordSection;
    private readonly ComboBox _wakeWordSensitivity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly NumericUpDown _wakeCommandSilenceMs = new()
    {
        Minimum = 800,
        Maximum = 8000,
        Increment = 200,
        Width = 100,
    };
    private readonly Label _wakeSilenceHint = new()
    {
        AutoSize = true,
        MaximumSize = new Size(360, 0),
        ForeColor = SystemColors.GrayText,
        Text = "说完指令后，连续静音达到该时长即提交（不含 ASR 识别耗时）。",
    };
    private readonly ComboBox _hudCorner = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly HotkeyCaptureTextBox _pttHotkey = new() { Width = 200 };
    private readonly ComboBox _forcedIntent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _onRefineFailure = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly CheckedListBox _optionalOverlaySkills = new() { Width = 360, Height = 72, CheckOnClick = true };
    private readonly Button _testConnection = new() { Text = "测试连接", Width = 100 };
    private readonly Label _testResult = new()
    {
        AutoSize = false,
        ForeColor = SystemColors.GrayText,
        TextAlign = ContentAlignment.TopLeft,
        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
    };

    public AppSettings Settings { get; private set; }

    /// <summary>When set, reflects the live tray mode instead of stale persisted settings.</summary>
    public VoiceTriggerMode? RuntimeTriggerMode { get; set; }

    public SettingsForm(
        AppSettings settings,
        IAudioDeviceEnumerator? deviceEnumerator = null,
        IUserEnrollmentService? enrollment = null,
        IEnrollmentUtteranceSource? enrollmentCapture = null,
        bool speakerModelMissing = false)
    {
        _deviceEnumerator = deviceEnumerator;
        _enrollment = enrollment;
        _enrollmentCapture = enrollmentCapture;
        _speakerModelMissing = speakerModelMissing;
        Settings = settings;
        Settings.MigrateLegacyApiSettings();
        Text = $"Array Mic Refreshment — 设置 ({AppInfo.Version})";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 860);

        foreach (var opt in TriggerModeOptions.All)
        {
            _triggerMode.Items.Add(opt);
        }

        foreach (var opt in WakeWordSensitivityOptions.All)
        {
            _wakeWordSensitivity.Items.Add(opt);
        }

        foreach (var opt in HudCornerOptions.All)
        {
            _hudCorner.Items.Add(opt);
        }

        foreach (var opt in SkillOptions.All)
        {
            _forcedIntent.Items.Add(opt);
        }

        foreach (OnRefineFailure value in Enum.GetValues<OnRefineFailure>())
        {
            _onRefineFailure.Items.Add(value);
        }

        // Preset combo: populate with the 3 presets
        _suppressPresetComboEvents = true;
        try
        {
            for (int i = 0; i < Settings.LlmPresets.Count; i++)
            {
                _llmPresetCombo.Items.Add($"预设{i + 1}: {Settings.LlmPresets[i].Name}");
            }

            if (_llmPresetCombo.Items.Count > 0)
            {
                var maxIndex = _llmPresetCombo.Items.Count - 1;
                _llmPresetCombo.SelectedIndex = Math.Clamp(Settings.SelectedLlmPresetIndex, 0, maxIndex);
            }

            _previousPresetIndex = _llmPresetCombo.SelectedIndex;
        }
        finally
        {
            _suppressPresetComboEvents = false;
        }

        _llmPresetCombo.SelectedIndexChanged += OnLlmPresetChanged;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 23,
            Padding = new Padding(12),
            AutoScroll = true,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        _settingsLayout = layout;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < layout.RowCount; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.RowStyles[22] = new RowStyle(SizeType.Absolute, 56);

        void AddRow(int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        void AddFullWidthRow(int row, Control control)
        {
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 2);
        }

        var speakerButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
        };
        speakerButtons.Controls.Add(_currentUserCombo);
        speakerButtons.Controls.Add(_addUserButton);
        speakerButtons.Controls.Add(_deleteUserButton);

        AddRow(0, "录音设备", _deviceCombo);
        AddRow(1, "当前用户", speakerButtons);
        AddRow(2, "声纹阈值", _speakerThreshold);
        AddFullWidthRow(3, _speakerStatus);

        var asrModelPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
        };
        asrModelPanel.Controls.Add(_asrModelCombo);
        asrModelPanel.Controls.Add(_downloadModelButton);
        asrModelPanel.Controls.Add(_downloadProgress);
        AddRow(4, "ASR 模型", asrModelPanel);
        AddFullWidthRow(5, _asrModelStatus);
        AddRow(6, "LLM 预设", _llmPresetCombo);
        AddRow(7, "预设名称", _llmPresetName);
        AddRow(8, "API Base URL", _apiUrl);
        AddRow(9, "API Key", _apiKey);
        AddRow(10, "Model", _apiModel);
        AddFullWidthRow(11, _refineEnabled);
        AddRow(12, "开机自启", _launchAtStartup);
        AddRow(13, "Skills 目录", _skillsDir);
        AddRow(14, "触发模式", _triggerMode);

        var wakeWordSection = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        wakeWordSection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        wakeWordSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var wakeRow = 0; wakeRow < 3; wakeRow++)
        {
            wakeWordSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        wakeWordSection.Controls.Add(new Label { Text = "唤醒词文本", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        wakeWordSection.Controls.Add(_wakeWordPhrase, 1, 0);
        wakeWordSection.Controls.Add(new Label { Text = "唤醒灵敏度", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        wakeWordSection.Controls.Add(_wakeWordSensitivity, 1, 1);

        var wakeSilencePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        wakeSilencePanel.Controls.Add(_wakeCommandSilenceMs);
        wakeSilencePanel.Controls.Add(_wakeSilenceHint);
        wakeWordSection.Controls.Add(new Label { Text = "指令结束静音", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        wakeWordSection.Controls.Add(wakeSilencePanel, 1, 2);
        _wakeWordSection = wakeWordSection;
        AddFullWidthRow(15, wakeWordSection);

        AddRow(16, "HUD 位置", _hudCorner);
        var hotkeyPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        hotkeyPanel.Controls.Add(_pttHotkey);
        hotkeyPanel.Controls.Add(new Label
        {
            Text = "点击输入框后按下组合键（勿用键盘输入文字）",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(360, 0),
        });
        AddRow(17, "PTT 热键", hotkeyPanel);
        AddRow(18, "整理风格", _forcedIntent);
        AddRow(19, "整理失败时", _onRefineFailure);
        AddRow(20, "附加叠加 skill", _optionalOverlaySkills);
        AddFullWidthRow(21, _testConnection);
        AddFullWidthRow(22, _testResult);

        _triggerMode.SelectedIndexChanged += (_, _) => UpdateWakeWordUiVisibility();
        _refineEnabled.CheckedChanged += OnRefineEnabledChanged;
        _testConnection.Click += OnTestConnectionClick;
        _currentUserCombo.SelectedIndexChanged += OnCurrentUserChanged;
        _addUserButton.Click += OnAddUserClick;
        _deleteUserButton.Click += OnDeleteUserClick;
        _asrModelCombo.SelectedIndexChanged += OnAsrModelChanged;
        _downloadModelButton.Click += OnDownloadModelClick;

        var enrollmentAvailable = _enrollment is not null;
        _addUserButton.Enabled = enrollmentAvailable;
        _deleteUserButton.Enabled = enrollmentAvailable;
        _currentUserCombo.Enabled = enrollmentAvailable;

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8, 6, 8, 6) };
        var versionLabel = new Label
        {
            Text = AppInfo.Version,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            Location = new Point(8, 14),
        };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
        };
        // Do not set DialogResult on OK — that can close/dispose the form before validation finishes.
        var ok = new Button { Text = "确定" };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (_testConnectionRunning)
            {
                MessageBox.Show(this, "正在测试连接，请稍候。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (TryApplyWithPrivacy())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(versionLabel);
        footer.Controls.Add(buttons);

        Controls.Add(layout);
        Controls.Add(footer);
        AcceptButton = ok;
        CancelButton = cancel;

        LoadFromSettings();
        UpdateWakeWordUiVisibility();
        ReloadOptionalSkillsList();
        LoadDeviceCombo();
        ReloadSpeakerUsers();
        UpdateSpeakerStatusLabel();
        ReloadAsrModels();

        Shown += (_, _) => LoadDeviceCombo();
    }

    private void UpdateSpeakerStatusLabel()
    {
        if (_speakerModelMissing)
        {
            _speakerStatus.Text =
                "未找到说话人 ONNX 模型，声纹校验不可用。运行 scripts\\download-models.ps1 后重启。";
            return;
        }

        if (_enrollment is null)
        {
            _speakerStatus.Text = "说话人模块未加载。";
            return;
        }

        var userCount = _enrollment.ListEnrolledUsers().Count;
        if (userCount == 0)
        {
            _speakerStatus.Text = "尚未注册说话人。点击「新增用户」或托盘「注册说话人…」录入 3 段语音。";
            return;
        }

        if (_currentUserCombo.SelectedItem is EnrolledUser user && user.IsNone)
        {
            _speakerStatus.Text =
                $"已注册 {userCount} 人。当前为「(无)」— 不校验声纹。请选择用户以仅允许本人转写。";
            return;
        }

        _speakerStatus.Text =
            $"已注册 {userCount} 人。声纹相似度 ≥ 阈值时才会转写并粘贴。";
    }

    private void ReloadAsrModels()
    {
        _asrModelCombo.Items.Clear();
        var allModels = AsrModelInfo.All;

        foreach (var model in allModels)
        {
            _asrModelCombo.Items.Add(model);
        }

        _asrModelCombo.DisplayMember = nameof(AsrModelInfo.DisplayName);
        _asrModelCombo.ValueMember = nameof(AsrModelInfo.Id);

        if (_asrModelCombo.Items.Count == 0)
        {
            UpdateAsrModelStatus();
            return;
        }

        // Priority: 1) user saved preference, 2) first installed, 3) first available
        var available = SenseVoiceModelResolver.ListAvailableModels(Settings.ModelsDirectory);
        var installedIds = available.Select(a => a.Id).ToHashSet();

        if (!string.IsNullOrWhiteSpace(Settings.SelectedAsrModelId)
            && TrySelectAsrModelById(Settings.SelectedAsrModelId))
        {
            UpdateAsrModelStatus();
            return;
        }

        var firstInstalled = allModels.FirstOrDefault(m => installedIds.Contains(m.Id));
        if (firstInstalled is not null && TrySelectAsrModelById(firstInstalled.Id))
        {
            UpdateAsrModelStatus();
            return;
        }

        _asrModelCombo.SelectedIndex = 0;
        UpdateAsrModelStatus();
    }

    private bool TrySelectAsrModelById(string modelId)
    {
        for (var i = 0; i < _asrModelCombo.Items.Count; i++)
        {
            if (_asrModelCombo.Items[i] is AsrModelInfo m && m.Id == modelId)
            {
                _asrModelCombo.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }

    private void OnAsrModelChanged(object? sender, EventArgs e) => UpdateAsrModelStatus();

    private void UpdateAsrModelStatus()
    {
        if (_asrModelCombo.SelectedItem is not AsrModelInfo model)
        {
            _asrModelStatus.Text = "";
            _downloadModelButton.Text = "下载";
            _downloadModelButton.Enabled = false;
            return;
        }

        var available = SenseVoiceModelResolver.ListAvailableModels(Settings.ModelsDirectory);
        var isInstalled = available.Any(a => a.Id == model.Id);
        var status = isInstalled ? "✓ 已安装" : "✗ 未安装";
        var punctuation = model.HasPunctuation ? "支持标点" : "无标点";
        var speed = model.IsQuantized ? "速度快" : "精度高·速度较慢";

        _asrModelStatus.Text = $"{status} | {punctuation} | {speed} — {model.Description}";
        _downloadModelButton.Text = isInstalled ? "已安装" : "下载";
        _downloadModelButton.Enabled = !isInstalled;
    }

    private async void OnDownloadModelClick(object? sender, EventArgs e)
    {
        if (_asrModelCombo.SelectedItem is not AsrModelInfo model)
        {
            return;
        }

        _downloadModelButton.Enabled = false;
        _asrModelCombo.Enabled = false;
        _downloadProgress.Visible = true;
        _downloadProgress.Value = 0;

        try
        {
            var service = new ModelDownloadService();
            var progress = new Progress<DownloadProgress>(p =>
            {
                _downloadProgress.Value = Math.Min(p.Percent, 100);
                _asrModelStatus.Text = p.Message;
                if (p.IsComplete)
                {
                    _downloadModelButton.Text = "已安装";
                    _downloadModelButton.Enabled = false;
                    _downloadProgress.Visible = false;
                }
            });

            await service.DownloadModelAsync(Settings.ModelsDirectory, model.Id, progress);

            // Refresh UI after download
            ReloadAsrModels();
            TrySelectAsrModelById(model.Id);
            UpdateAsrModelStatus();
        }
        catch (Exception ex)
        {
            _asrModelStatus.Text = $"下载失败: {ex.Message}";
            _downloadModelButton.Enabled = true;

            // Clean up incomplete download on failure so user can retry
            try
            {
                var archivePath = Path.Combine(
                    ModelsPathResolver.Resolve(Settings.ModelsDirectory),
                    ".cache",
                    $"{model.Id}.tar.bz2");
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }
                var tempPath = archivePath + ".tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
        finally
        {
            _asrModelCombo.Enabled = true;
        }
    }

    private void LoadDeviceCombo()
    {
        _deviceCombo.Items.Clear();
        _deviceCombo.Enabled = false;
        if (_deviceEnumerator is null)
        {
            _deviceCombo.Items.Add("(本机无设备枚举)");
            _deviceCombo.SelectedIndex = 0;
            return;
        }

        _deviceItems = DeviceComboPopulator.BuildItems(_deviceEnumerator);
        foreach (var item in _deviceItems)
        {
            _deviceCombo.Items.Add(item);
        }

        _deviceCombo.DisplayMember = nameof(DeviceComboPopulator.DeviceListItem.DisplayName);
        _deviceCombo.ValueMember = nameof(DeviceComboPopulator.DeviceListItem.Id);
        _deviceCombo.Enabled = _deviceItems.Count > 0;
        var index = DeviceComboPopulator.ResolveSelectedIndex(_deviceItems, Settings.SelectedDeviceId);
        if (index >= 0)
        {
            _deviceCombo.SelectedIndex = index;
        }
    }

    private void ReloadSpeakerUsers()
    {
        _currentUserCombo.Items.Clear();
        if (_enrollment is null)
        {
            _currentUserCombo.Items.Add("(未启用说话人门禁)");
            _currentUserCombo.SelectedIndex = 0;
            return;
        }

        _currentUserCombo.Items.Add(EnrolledUser.None);
        foreach (var user in _enrollment.ListEnrolledUsers())
        {
            _currentUserCombo.Items.Add(user);
        }

        _currentUserCombo.DisplayMember = nameof(EnrolledUser.Name);
        _currentUserCombo.ValueMember = nameof(EnrolledUser.Id);

        if (_currentUserCombo.Items.Count == 0)
        {
            return;
        }

        var selectedId = Settings.CurrentSpeakerUserId ?? _enrollment.CurrentUserId;
        var index = 0;
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            for (var i = 0; i < _currentUserCombo.Items.Count; i++)
            {
                if (_currentUserCombo.Items[i] is EnrolledUser u
                    && string.Equals(u.Id, selectedId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }
        }

        _currentUserCombo.SelectedIndex = index;
        UpdateSpeakerStatusLabel();
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        if (_enrollment is null || _currentUserCombo.SelectedItem is not EnrolledUser user)
        {
            return;
        }

        if (user.IsNone)
        {
            _enrollment.CurrentUserId = null;
            Settings.CurrentSpeakerUserId = null;
            return;
        }

        _enrollment.SetCurrentUser(user.Id);
        Settings.CurrentSpeakerUserId = user.Id;
        UpdateSpeakerStatusLabel();
    }

    private void OnAddUserClick(object? sender, EventArgs e)
    {
        if (_enrollment is null)
        {
            return;
        }

        using var dialog = new EnrollmentDialog(_enrollment, _enrollmentCapture);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ReloadSpeakerUsers();
            UpdateSpeakerStatusLabel();
        }
    }

    private void OnDeleteUserClick(object? sender, EventArgs e)
    {
        if (_enrollment is null
            || _currentUserCombo.SelectedItem is not EnrolledUser user
            || user.IsNone)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"确定删除用户「{user.Name}」？",
            "删除用户",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _enrollment.DeleteUser(user.Id);
        ReloadSpeakerUsers();
        UpdateSpeakerStatusLabel();
    }

    private void OnRefineEnabledChanged(object? sender, EventArgs e)
    {
        if (_refineEnabled.Checked)
        {
            _ = PrivacyConsent.EnsureAccepted(Settings, _apiUrl.Text.Trim(), this);
        }
    }

    private bool TryApplyWithPrivacy()
    {
        try
        {
            var hotkey = _pttHotkey.HotkeyExpression;
            if (!HotkeyParser.TryParse(hotkey, out _, out var hotkeyError))
            {
                MessageBox.Show(this, hotkeyError ?? "PTT 热键无效。", "热键", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _pttHotkey.Focus();
                return false;
            }

            var triggerMode = ResolveTriggerModeFromUi();
            if (triggerMode == VoiceTriggerMode.WakeWordOnly && string.IsNullOrWhiteSpace(_wakeWordPhrase.Text))
            {
                MessageBox.Show(
                    this,
                    "已选择「唤醒词」触发模式，请填写唤醒词文本。",
                    "唤醒词",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _wakeWordPhrase.Focus();
                return false;
            }

            // Block saving if user selected a model that is not installed
            if (_asrModelCombo.SelectedItem is AsrModelInfo selectedModel)
            {
                var available = SenseVoiceModelResolver.ListAvailableModels(Settings.ModelsDirectory);
                var isInstalled = available.Any(a => a.Id == selectedModel.Id);
                if (!isInstalled)
                {
                    MessageBox.Show(
                        this,
                        $"你选择的 ASR 模型「{selectedModel.DisplayName}」尚未安装。\n\n" +
                        $"请点击「下载」按钮安装后再试，或切换到已安装的模型。",
                        "模型未安装",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _asrModelCombo.Focus();
                    return false;
                }
            }

            var draft = BuildDraftSettings();
            draft.ApiBaseUrl = ApiUrlNormalizer.NormalizeBaseUrl(draft.ApiBaseUrl);

            if (draft.PromptRefineEnabled)
            {
                if (string.IsNullOrWhiteSpace(draft.ApiBaseUrl))
                {
                    MessageBox.Show(
                        this,
                        "已启用「提示词整理」，但未填写 API Base URL。\n\n请填写 API 地址后保存。",
                        "提示词整理",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _apiUrl.Focus();
                    return false;
                }

                var isLoopback = PrivacyConfirmation.TryResolveHost(draft.ApiBaseUrl, out var apiHost)
                    && PrivacyConfirmation.IsLoopbackHost(apiHost);
                if (string.IsNullOrWhiteSpace(draft.ApiKey) && !isLoopback)
                {
                    MessageBox.Show(
                        this,
                        "已启用「提示词整理」，但未填写 API Key。\n\n请填写 API Key 后保存，或改用本机 Ollama 地址（如 http://127.0.0.1:11434/v1）。",
                        "提示词整理",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _apiKey.Focus();
                    return false;
                }

                if (PrivacyConfirmation.ShouldPromptForHost(draft.ApiBaseUrl, draft.PrivacyAcceptedHost))
                {
                    if (!PrivacyConsent.EnsureAccepted(draft, draft.ApiBaseUrl, this))
                    {
                        _refineEnabled.Checked = false;
                        _refineEnabled.Focus();
                        return false;
                    }
                }
                else if (PrivacyConfirmation.TryResolveHost(draft.ApiBaseUrl, out var host))
                {
                    draft.PrivacyAcceptedHost = host;
                }
            }

            Settings = draft;
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to commit settings from dialog");
            MessageBox.Show(
                this,
                $"保存设置时出错：{ex.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    private void UpdateWakeWordUiVisibility()
    {
        var mode = (_triggerMode.SelectedItem as TriggerModeOption)?.Value ?? VoiceTriggerMode.PttOnly;
        var wake = mode is VoiceTriggerMode.WakeWordOnly or VoiceTriggerMode.Both;
        if (_wakeWordSection is not null)
        {
            _wakeWordSection.Visible = wake;
        }

        if (_settingsLayout is not null)
        {
            _settingsLayout.RowStyles[WakeWordSectionRow] = wake
                ? new RowStyle(SizeType.AutoSize)
                : new RowStyle(SizeType.Absolute, 0);
            _settingsLayout.PerformLayout();
        }

        _wakeWordPhrase.Enabled = wake;
        _wakeWordSensitivity.Enabled = wake;
        _wakeCommandSilenceMs.Enabled = wake;
    }

    private void LoadFromSettings()
    {
        LoadCurrentPresetIntoUi();
        _refineEnabled.Checked = Settings.PromptRefineEnabled;
        _launchAtStartup.Checked = Settings.LaunchAtStartup;
        _skillsDir.Text = Settings.SkillsDirectory;
        var triggerMode = RuntimeTriggerMode ?? Settings.TriggerMode;
        for (var i = 0; i < TriggerModeOptions.All.Count; i++)
        {
            if (TriggerModeOptions.All[i].Value != triggerMode)
            {
                continue;
            }

            _triggerMode.SelectedIndex = i;
            break;
        }

        _loadedTriggerMode = triggerMode;
        _wakeWordPhrase.Text = Settings.WakeWordPhrase;
        SelectWakeSensitivity(Settings.WakeWordSensitivity);
        _wakeCommandSilenceMs.Value = Math.Clamp(Settings.WakeCommandSilenceMs, 800, 8000);
        SelectHudCorner(Settings.HudScreenCorner);
        _pttHotkey.HotkeyExpression = Settings.PttHotkey;
        var forced = SkillOptions.All.FirstOrDefault(x => x.Value == Settings.ForcedIntent)
            ?? SkillOptions.All.First(x => x.Value == PromptIntent.PlainText);
        _forcedIntent.SelectedItem = forced;
        SelectComboEnum(_onRefineFailure, Settings.OnRefineFailure);
        _speakerThreshold.Value = (decimal)Math.Clamp(Settings.SpeakerVerifyThreshold, 0.25f, 0.85f);
    }

    private void LoadCurrentPresetIntoUi()
    {
        if (Settings?.LlmPresets is null || Settings.LlmPresets.Count == 0)
        {
            return;
        }

        var idx = Math.Clamp(_llmPresetCombo.SelectedIndex, 0, Settings.LlmPresets.Count - 1);
        if (idx < 0 || idx >= Settings.LlmPresets.Count)
        {
            return;
        }

        var preset = Settings.LlmPresets[idx];
        _llmPresetName.Text = preset.Name;
        _apiUrl.Text = preset.ApiBaseUrl;
        _apiKey.Text = preset.ApiKey;
        _apiModel.Text = preset.ApiModel;
    }

    private void SaveUiIntoCurrentPreset()
    {
        if (Settings?.LlmPresets is null || Settings.LlmPresets.Count == 0)
        {
            return;
        }

        var idx = Math.Clamp(_llmPresetCombo.SelectedIndex, 0, Settings.LlmPresets.Count - 1);
        if (idx < 0 || idx >= Settings.LlmPresets.Count)
        {
            return;
        }

        var preset = Settings.LlmPresets[idx];
        preset.Name = _llmPresetName.Text.Trim();
        preset.ApiBaseUrl = _apiUrl.Text.Trim();
        preset.ApiKey = _apiKey.Text;
        preset.ApiModel = _apiModel.Text.Trim();
        UpdatePresetComboLabel(idx, preset.Name);
    }

    private void UpdatePresetComboLabel(int idx, string presetName)
    {
        if (idx < 0 || idx >= _llmPresetCombo.Items.Count)
        {
            return;
        }

        _suppressPresetComboEvents = true;
        try
        {
            _llmPresetCombo.Items[idx] = $"预设{idx + 1}: {presetName}";
        }
        finally
        {
            _suppressPresetComboEvents = false;
        }
    }

    private void SelectWakeSensitivity(WakeWordSensitivity value)
    {
        for (var i = 0; i < WakeWordSensitivityOptions.All.Count; i++)
        {
            if (WakeWordSensitivityOptions.All[i].Value == value)
            {
                _wakeWordSensitivity.SelectedIndex = i;
                return;
            }
        }

        if (_wakeWordSensitivity.Items.Count > 0)
        {
            _wakeWordSensitivity.SelectedIndex = 0;
        }
    }

    private void SelectHudCorner(HudScreenCorner value)
    {
        for (var i = 0; i < HudCornerOptions.All.Count; i++)
        {
            if (HudCornerOptions.All[i].Value == value)
            {
                _hudCorner.SelectedIndex = i;
                return;
            }
        }

        if (_hudCorner.Items.Count > 0)
        {
            _hudCorner.SelectedIndex = 0;
        }
    }

    private static void SelectComboEnum<T>(ComboBox combo, T value) where T : struct, Enum
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is T item && EqualityComparer<T>.Default.Equals(item, value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
    }

    private void OnLlmPresetChanged(object? sender, EventArgs e)
    {
        if (_suppressPresetComboEvents
            || Settings?.LlmPresets is null
            || Settings.LlmPresets.Count == 0)
        {
            return;
        }

        // Save the OLD preset (using the tracked previous index, because
        // SelectedIndex has already changed to the NEW value by the time
        // this event fires).
        if (_previousPresetIndex >= 0 && _previousPresetIndex < Settings.LlmPresets.Count)
        {
            var oldPreset = Settings.LlmPresets[_previousPresetIndex];
            oldPreset.Name = _llmPresetName.Text.Trim();
            oldPreset.ApiBaseUrl = _apiUrl.Text.Trim();
            oldPreset.ApiKey = _apiKey.Text;
            oldPreset.ApiModel = _apiModel.Text.Trim();
            UpdatePresetComboLabel(_previousPresetIndex, oldPreset.Name);
        }

        // Load the NEW preset
        LoadCurrentPresetIntoUi();

        // Remember the new index for the next switch
        _previousPresetIndex = _llmPresetCombo.SelectedIndex;
    }

    private void ReloadOptionalSkillsList()
    {
        _optionalOverlaySkills.Items.Clear();
        try
        {
            var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(_skillsDir.Text.Trim()));
            foreach (var entry in catalog.OptionalSkills)
            {
                _optionalOverlaySkills.Items.Add(
                    entry.Key,
                    Settings.OptionalOverlaySkills.Contains(entry.Key, StringComparer.OrdinalIgnoreCase));
            }

            if (catalog.MissingFiles.Count > 0)
            {
                _testResult.Text = $"缺少 skill 文件: {string.Join(", ", catalog.MissingFiles)}";
            }
        }
        catch (Exception ex)
        {
            _testResult.Text = $"Skills 目录错误: {ex.Message}";
        }
    }

    private AppSettings BuildDraftSettings()
    {
        var overlay = new List<string>();
        for (var i = 0; i < _optionalOverlaySkills.Items.Count; i++)
        {
            if (_optionalOverlaySkills.GetItemChecked(i) && _optionalOverlaySkills.Items[i] is string key)
            {
                overlay.Add(key);
            }
        }

        string? deviceId = Settings.SelectedDeviceId;
        if (_deviceEnumerator is not null && _deviceItems.Count > 0 && _deviceCombo.SelectedIndex >= 0)
        {
            deviceId = DeviceComboPopulator.GetSelectedDeviceId(_deviceItems, _deviceCombo.SelectedIndex);
        }

        string? speakerUserId = Settings.CurrentSpeakerUserId;
        if (_enrollment is not null && _currentUserCombo.SelectedItem is EnrolledUser user && !user.IsNone)
        {
            speakerUserId = user.Id;
        }
        else if (_enrollment is not null && _currentUserCombo.SelectedItem is EnrolledUser none && none.IsNone)
        {
            speakerUserId = null;
        }

        var selectedAsrModel = _asrModelCombo.SelectedItem is AsrModelInfo mi ? mi.Id : string.Empty;

        // Ensure the current preset is up-to-date before returning
        SaveUiIntoCurrentPreset();
        var presetIdx = Math.Clamp(_llmPresetCombo.SelectedIndex, 0, Math.Max(0, (Settings.LlmPresets?.Count ?? 1) - 1));
        var currentPreset = (Settings.LlmPresets != null && presetIdx >= 0 && presetIdx < Settings.LlmPresets.Count)
            ? Settings.LlmPresets[presetIdx]
            : new LlmPreset();

        return new AppSettings
        {
            MasterEnabled = Settings.MasterEnabled,
            PasteToCaretEnabled = Settings.PasteToCaretEnabled,
            LaunchAtStartup = _launchAtStartup.Checked,
            PromptRefineEnabled = _refineEnabled.Checked,
            ForcedIntent = (_forcedIntent.SelectedItem as SkillOption)?.Value ?? PromptIntent.Auto,
            OnRefineFailure = (OnRefineFailure)(_onRefineFailure.SelectedItem ?? OnRefineFailure.UseRawTranscript),
            SelectedDeviceId = deviceId,
            CurrentSpeakerUserId = speakerUserId,
            PttHotkey = _pttHotkey.HotkeyExpression,
            TriggerMode = ResolveTriggerModeFromUi(),
            WakeWordPhrase = _wakeWordPhrase.Text.Trim(),
            WakeWordSensitivity = (_wakeWordSensitivity.SelectedItem as WakeWordSensitivityOption)?.Value
                ?? WakeWordSensitivity.High,
            WakeCommandSilenceMs = (int)_wakeCommandSilenceMs.Value,
            HudScreenCorner = (_hudCorner.SelectedItem as HudCornerOption)?.Value
                ?? HudScreenCorner.BottomRight,
            SkillsDirectory = _skillsDir.Text.Trim(),
            ModelsDirectory = Settings.ModelsDirectory,
            SpeakerVerifyThreshold = (float)_speakerThreshold.Value,
            PrivacyAcceptedHost = Settings.PrivacyAcceptedHost,
            OptionalOverlaySkills = overlay,
            SelectedAsrModelId = selectedAsrModel,
            SelectedLlmPresetIndex = presetIdx,
            LlmPresets = Settings.LlmPresets?.Select(p => new LlmPreset
            {
                Name = p.Name,
                ApiBaseUrl = p.ApiBaseUrl,
                ApiKey = p.ApiKey,
                ApiModel = p.ApiModel,
            }).ToList() ?? new List<LlmPreset>(),
            // Keep legacy fields in sync
            ApiBaseUrl = currentPreset.ApiBaseUrl,
            ApiKey = currentPreset.ApiKey,
            ApiModel = currentPreset.ApiModel,
        };
    }

    private VoiceTriggerMode ResolveTriggerModeFromUi()
    {
        if (_triggerMode.SelectedItem is TriggerModeOption selected)
        {
            return selected.Value;
        }

        if (_triggerMode.SelectedIndex >= 0 && _triggerMode.SelectedIndex < TriggerModeOptions.All.Count)
        {
            return TriggerModeOptions.All[_triggerMode.SelectedIndex].Value;
        }

        return _loadedTriggerMode;
    }

    private async void OnTestConnectionClick(object? sender, EventArgs e)
    {
        if (_testConnectionRunning || IsDisposed)
        {
            return;
        }

        _testConnectionRunning = true;
        SetTestUiState(busy: true, resultText: "测试中…");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var draft = BuildDraftSettings();
            draft.PromptRefineEnabled = true;
            draft.ApiBaseUrl = ApiUrlNormalizer.NormalizeBaseUrl(draft.ApiBaseUrl);

            if (string.IsNullOrWhiteSpace(draft.ApiBaseUrl))
            {
                SetTestUiState(busy: false, resultText: "失败：请先填写 API Base URL");
                return;
            }

            if (!PrivacyConsent.EnsureAccepted(draft, draft.ApiBaseUrl, this))
            {
                SetTestUiState(busy: false, resultText: "已取消（隐私未确认）");
                return;
            }

            var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(draft.SkillsDirectory));
            if (catalog.MissingFiles.Count > 0)
            {
                SetTestUiState(busy: false, resultText: $"失败：缺少文件 {string.Join(", ", catalog.MissingFiles)}");
                return;
            }

            var router = new OpenAiCompatibleIntentRouter(draft, catalog);
            var refiner = new OpenAiCompatiblePromptRefiner(draft, catalog);
            var sample = "ping connectivity test";
            var (_, confidence) = await router.RouteAsync(sample, CancellationToken.None).ConfigureAwait(true);
            if (IsDisposed)
            {
                return;
            }

            _ = await refiner.RefineAsync(sample, PromptIntent.GeneralAi, CancellationToken.None).ConfigureAwait(true);
            if (IsDisposed)
            {
                return;
            }

            sw.Stop();
            SetTestUiState(busy: false, resultText: $"成功（{sw.ElapsedMilliseconds} ms，router confidence={confidence:F2}）");
        }
        catch (RefineApiException ex)
        {
            sw.Stop();
            SetTestUiState(busy: false, resultText: $"失败（{sw.ElapsedMilliseconds} ms）: {ex.Message}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            SetTestUiState(busy: false, resultText: $"失败（{sw.ElapsedMilliseconds} ms）: {ex.Message}");
            try
            {
                var diag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "amr_test_crash.txt");
                File.WriteAllText(diag, $"[{DateTime.Now}] Test connection error:\n{ex}\n\nStackTrace:\n{ex.StackTrace}");
            }
            catch
            {
                // ignore
            }
        }
        finally
        {
            _testConnectionRunning = false;
            if (!IsDisposed)
            {
                _testConnection.Enabled = true;
            }
        }
    }

    private void SetTestUiState(bool busy, string resultText)
    {
        if (IsDisposed)
        {
            return;
        }

        _testConnection.Enabled = !busy;
        _testResult.Text = resultText;
    }
}
