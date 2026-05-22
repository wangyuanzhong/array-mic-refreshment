using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App;

public sealed class SettingsForm : Form
{
    private readonly IAudioDeviceEnumerator? _deviceEnumerator;
    private readonly IUserEnrollmentService? _enrollment;
    private readonly IEnrollmentUtteranceSource? _enrollmentCapture;
    private IReadOnlyList<DeviceComboPopulator.DeviceListItem> _deviceItems = Array.Empty<DeviceComboPopulator.DeviceListItem>();

    private readonly ComboBox _deviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ComboBox _currentUserCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _addUserButton = new() { Text = "新增用户…", AutoSize = true };
    private readonly Button _deleteUserButton = new() { Text = "删除", AutoSize = true, Width = 72 };
    private readonly CheckBox _refineEnabled = new() { Text = "启用提示词整理（默认建议关闭）", AutoSize = true };
    private readonly TextBox _apiUrl = new() { Width = 360 };
    private readonly TextBox _apiKey = new() { Width = 360, UseSystemPasswordChar = true };
    private readonly TextBox _apiModel = new() { Width = 360 };
    private readonly TextBox _skillsDir = new() { Width = 360 };
    private readonly TextBox _pttHotkey = new() { Width = 200 };
    private readonly ComboBox _forcedIntent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _onRefineFailure = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly CheckedListBox _optionalOverlaySkills = new() { Width = 360, Height = 72, CheckOnClick = true };
    private readonly Button _testConnection = new() { Text = "测试连接", Width = 100 };
    private readonly Label _testResult = new() { AutoSize = true, MaximumSize = new Size(360, 0) };

    public AppSettings Settings { get; private set; }

    public SettingsForm(
        AppSettings settings,
        IAudioDeviceEnumerator? deviceEnumerator = null,
        IUserEnrollmentService? enrollment = null,
        IEnrollmentUtteranceSource? enrollmentCapture = null)
    {
        _deviceEnumerator = deviceEnumerator;
        _enrollment = enrollment;
        _enrollmentCapture = enrollmentCapture;
        Settings = settings;
        Text = "Array Mic Refreshment — 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 560);

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
            RowCount = 14,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddRow(int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        var speakerButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
        };
        speakerButtons.Controls.Add(_currentUserCombo);
        speakerButtons.Controls.Add(_addUserButton);
        speakerButtons.Controls.Add(_deleteUserButton);

        AddRow(0, "录音设备", _deviceCombo);
        AddRow(1, "当前用户", speakerButtons);
        AddRow(2, "API Base URL", _apiUrl);
        AddRow(3, "API Key", _apiKey);
        AddRow(4, "Model", _apiModel);
        AddRow(5, "", _refineEnabled);
        layout.SetColumnSpan(_refineEnabled, 2);
        AddRow(6, "Skills 目录", _skillsDir);
        AddRow(7, "PTT 热键", _pttHotkey);
        AddRow(8, "强制意图", _forcedIntent);
        AddRow(9, "整理失败时", _onRefineFailure);
        AddRow(10, "附加叠加 skill", _optionalOverlaySkills);
        AddRow(11, "", _testConnection);
        layout.SetColumnSpan(_testConnection, 2);
        AddRow(12, "", _testResult);
        layout.SetColumnSpan(_testResult, 2);

        _refineEnabled.CheckedChanged += OnRefineEnabledChanged;
        _testConnection.Click += OnTestConnectionClick;
        _currentUserCombo.SelectedIndexChanged += OnCurrentUserChanged;
        _addUserButton.Click += OnAddUserClick;
        _deleteUserButton.Click += OnDeleteUserClick;

        var enrollmentAvailable = _enrollment is not null;
        _addUserButton.Enabled = enrollmentAvailable;
        _deleteUserButton.Enabled = enrollmentAvailable;
        _currentUserCombo.Enabled = enrollmentAvailable;

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (!TryApplyWithPrivacy())
            {
                DialogResult = DialogResult.None;
            }
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        LoadFromSettings();
        ReloadOptionalSkillsList();
        LoadDeviceCombo();
        ReloadSpeakerUsers();
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
        var index = -1;
        for (var i = 0; i < _currentUserCombo.Items.Count; i++)
        {
            if (_currentUserCombo.Items[i] is EnrolledUser u
                && string.Equals(u.Id, selectedId, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        _currentUserCombo.SelectedIndex = index >= 0 ? index : 0;
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        if (_enrollment is null || _currentUserCombo.SelectedItem is not EnrolledUser user)
        {
            return;
        }

        _enrollment.SetCurrentUser(user.Id);
        Settings.CurrentSpeakerUserId = user.Id;
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
        }
    }

    private void OnDeleteUserClick(object? sender, EventArgs e)
    {
        if (_enrollment is null || _currentUserCombo.SelectedItem is not EnrolledUser user)
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
        var draft = BuildDraftSettings();
        if (draft.PromptRefineEnabled)
        {
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
        if (_enrollment is not null && _currentUserCombo.SelectedItem is EnrolledUser user)
        {
            speakerUserId = user.Id;
        }

        return new AppSettings
        {
            MasterEnabled = Settings.MasterEnabled,
            PasteToCaretEnabled = Settings.PasteToCaretEnabled,
            PromptRefineEnabled = _refineEnabled.Checked,
            ForcedIntent = (PromptIntent)(_forcedIntent.SelectedItem ?? PromptIntent.Auto),
            OnRefineFailure = (OnRefineFailure)(_onRefineFailure.SelectedItem ?? OnRefineFailure.UseRawTranscript),
            SelectedDeviceId = deviceId,
            CurrentSpeakerUserId = speakerUserId,
            PttHotkey = _pttHotkey.Text.Trim(),
            ApiBaseUrl = _apiUrl.Text.Trim(),
            ApiKey = _apiKey.Text,
            ApiModel = _apiModel.Text.Trim(),
            SkillsDirectory = _skillsDir.Text.Trim(),
            ModelsDirectory = Settings.ModelsDirectory,
            SpeakerVerifyThreshold = Settings.SpeakerVerifyThreshold,
            PrivacyAcceptedHost = Settings.PrivacyAcceptedHost,
            OptionalOverlaySkills = overlay,
        };
    }

    private async void OnTestConnectionClick(object? sender, EventArgs e)
    {
        _testConnection.Enabled = false;
        _testResult.Text = "测试中…";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var draft = BuildDraftSettings();
            draft.PromptRefineEnabled = true;
            if (!PrivacyConsent.EnsureAccepted(draft, draft.ApiBaseUrl, this))
            {
                _testResult.Text = "已取消（隐私未确认）";
                return;
            }

            var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(draft.SkillsDirectory));
            if (catalog.MissingFiles.Count > 0)
            {
                _testResult.Text = $"失败：缺少文件 {string.Join(", ", catalog.MissingFiles)}";
                return;
            }

            var router = new OpenAiCompatibleIntentRouter(draft, catalog);
            var refiner = new OpenAiCompatiblePromptRefiner(draft, catalog);
            var sample = "ping connectivity test";
            var (_, confidence) = await router.RouteAsync(sample, CancellationToken.None).ConfigureAwait(true);
            _ = await refiner.RefineAsync(sample, PromptIntent.GeneralAi, CancellationToken.None).ConfigureAwait(true);
            sw.Stop();
            _testResult.Text = $"成功（{sw.ElapsedMilliseconds} ms，router confidence={confidence:F2}）";
        }
        catch (RefineApiException ex)
        {
            sw.Stop();
            _testResult.Text = $"失败（{sw.ElapsedMilliseconds} ms）: {ex.Message}";
        }
        catch (Exception ex)
        {
            sw.Stop();
            _testResult.Text = $"失败（{sw.ElapsedMilliseconds} ms）: {ex.Message}";
        }
        finally
        {
            _testConnection.Enabled = true;
        }
    }
}
