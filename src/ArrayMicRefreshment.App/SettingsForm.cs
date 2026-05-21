using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

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
    private readonly CheckedListBox _optionalOverlaySkills = new() { Width = 360, Height = 72, CheckOnClick = true };
    private readonly Button _testConnection = new() { Text = "测试连接", Width = 100 };
    private readonly Label _testResult = new() { AutoSize = true, MaximumSize = new Size(360, 0) };

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Settings = settings;
        Text = "Array Mic Refreshment — 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 480);

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
            RowCount = 11,
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
        AddRow(8, "附加叠加 skill", _optionalOverlaySkills);
        AddRow(9, "", _testConnection);
        layout.SetColumnSpan(_testConnection, 2);
        AddRow(10, "", _testResult);
        layout.SetColumnSpan(_testResult, 2);

        _refineEnabled.CheckedChanged += OnRefineEnabledChanged;
        _testConnection.Click += OnTestConnectionClick;

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
        if (draft.PromptRefineEnabled && !PrivacyConsent.EnsureAccepted(draft, draft.ApiBaseUrl, this))
        {
            _refineEnabled.Checked = false;
            return false;
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

        return new AppSettings
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
