using System.Text.Json;
using ArrayMicRefreshment.App.Services;
using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Settings bridge methods (docs/UI_ROUTE_B_WEBVIEW2.md §7.2).</summary>
public sealed partial class WebUiBridge
{
    private readonly SettingsApplyService _settingsApplyService = new();

    public string GetRuntimeState()
    {
        return Serialize(new
        {
            triggerMode = (_context.RuntimeTriggerMode ?? _context.Settings.TriggerMode).ToString(),
            masterEnabled = _context.MasterEnabled ?? _context.Settings.MasterEnabled,
            speakerModelMissing = _context.SpeakerModelMissing,
            enrollmentAvailable = _context.Enrollment is not null,
            deviceEnumerationAvailable = _context.DeviceEnumerator is not null,
        });
    }

    public string ListAudioDevices()
    {
        var devices = SettingsMetadataProvider.ListAudioDevices(_context.DeviceEnumerator)
            .Select(d => new { id = d.Id, displayName = d.DisplayName, isDefault = d.IsDefault });
        return Serialize(devices);
    }

    public string ListAsrModels()
    {
        var models = SettingsMetadataProvider.ListAsrModels(_context.Settings.ModelsDirectory)
            .Select(m => new { id = m.Id, displayName = m.DisplayName, installed = m.Installed });
        return Serialize(models);
    }

    public string GetWakeWordModelStatus()
    {
        var status = SettingsMetadataProvider.GetWakeWordModelStatus(
            _context.Settings.ModelsDirectory,
            _context.Settings.WakeWordPhrase,
            _context.Settings.WakeWordSensitivity);
        return Serialize(new
        {
            displayName = status.DisplayName,
            installed = status.Installed,
            engineReady = status.EngineReady,
            resolvedPath = status.ResolvedPath,
            builtinPhrases = WakeWordPhraseEncoding.BuiltinPhrases,
        });
    }

    public string ListOptionalOverlaySkills()
    {
        var skills = SettingsMetadataProvider.ListOptionalOverlaySkills(
                _context.Settings.SkillsDirectory,
                _context.Settings.OptionalOverlaySkills)
            .Select(s => new { key = s.Key, label = s.Label, @checked = s.Checked });
        return Serialize(skills);
    }

    public string ListFeaturePresets()
    {
        _context.Settings.MigrateLegacyApiSettings();
        _context.Settings.MigrateLegacyFeaturePresets();

        var presets = _context.Settings.FeaturePresets
            .Select((p, i) => new
            {
                index = i,
                name = p.Name,
                llmPresetName = p.LlmPresetName,
                forcedIntent = p.ForcedIntent.ToString(),
                onRefineFailure = p.OnRefineFailure.ToString(),
                optionalOverlaySkills = p.OptionalOverlaySkills,
                selected = i == _context.Settings.SelectedFeaturePresetIndex,
            });
        return Serialize(presets);
    }

    public string ApplyFeaturePreset(int index)
    {
        return RunOnUiForJson(() => ApplyFeaturePresetCore(index));
    }

    private string ApplyFeaturePresetCore(int index)
    {
        _context.Settings.MigrateLegacyApiSettings();
        _context.Settings.MigrateLegacyFeaturePresets();

        if (_context.Settings.FeaturePresets is not { Count: > 0 })
        {
            return Serialize(new { ok = false, error = "未配置功能预设。" });
        }

        var previous = SettingsApplyService.CloneSnapshot(_context.Settings);
        FeaturePresetApplier.ApplyFeaturePreset(_context.Settings, index);

        if (_context.SettingsApplyHost is not null)
        {
            var applyService = _context.SettingsApplyService ?? _settingsApplyService;
            applyService.Apply(previous, _context.Settings, _context.SettingsApplyHost);
            return Serialize(new { ok = true, selectedFeaturePresetIndex = _context.Settings.SelectedFeaturePresetIndex });
        }

        _context.SettingsStore.Save(_context.Settings);
        return Serialize(new
        {
            ok = true,
            selectedFeaturePresetIndex = _context.Settings.SelectedFeaturePresetIndex,
            warning = "SettingsApplyHost 未配置：已写入 settings.json，但未应用 pipeline 运行时变更。",
        });
    }

    public string GetSkillsCatalogStatus()
    {
        var missing = SettingsMetadataProvider.GetSkillsCatalogMissingFiles(_context.Settings.SkillsDirectory);
        return Serialize(new { missingFiles = missing });
    }

    public string LoadSettingsDraft()
    {
        var draft = SettingsDraftMapper.ToDraft(_context.Settings, _context.RuntimeTriggerMode);
        return Serialize(draft);
    }

    public string ValidateSettingsDraft(string draftJson)
    {
        if (!TryParseDraft(draftJson, out var draft, out var parseError))
        {
            return Serialize(new SettingsValidationResultDto
            {
                Ok = false,
                Errors =
                [
                    new SettingsValidationErrorDto { Field = "_form", Message = parseError ?? "无效 JSON。" },
                ],
            });
        }

        return Serialize(SettingsDraftValidator.Validate(draft!, _context.Settings));
    }

    public string SaveSettingsDraft(string draftJson)
    {
        return RunOnUiForJson(() => SaveSettingsDraftCore(draftJson));
    }

    public string TestLlmConnection(string? draftJson)
    {
        AppSettings settings;
        if (!string.IsNullOrWhiteSpace(draftJson))
        {
            if (!TryParseDraft(draftJson, out var draft, out var parseError))
            {
                return Serialize(new LlmTestResultDto
                {
                    Ok = false,
                    Message = parseError ?? "无效 JSON。",
                });
            }

            settings = SettingsDraftMapper.ToAppSettings(draft!, _context.Settings);
        }
        else
        {
            settings = SettingsApplyService.CloneSnapshot(_context.Settings);
        }

        settings.PromptRefineEnabled = true;
        settings.ApiBaseUrl = ApiUrlNormalizer.NormalizeBaseUrl(settings.ApiBaseUrl);

        string? privacyOk = RunOnUiForJson(() =>
        {
            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            {
                return Serialize(new LlmTestResultDto { Ok = false, Message = "失败：请先填写 API Base URL" });
            }

            if (!PrivacyConsent.EnsureAccepted(settings, settings.ApiBaseUrl, _context.HostForm))
            {
                return Serialize(new LlmTestResultDto { Ok = false, Message = "已取消（隐私未确认）" });
            }

            return null;
        });

        if (privacyOk is not null)
        {
            return privacyOk;
        }

        try
        {
            var result = Task.Run(() => LlmConnectionTester.TestAsync(settings)).GetAwaiter().GetResult();
            return Serialize(result);
        }
        catch (Exception ex)
        {
            return Serialize(new LlmTestResultDto { Ok = false, Message = ex.Message });
        }
    }

    public string OpenHotkeyCaptureDialog(string currentHotkey)
    {
        return RunOnUiForJson(() =>
        {
            var result = HotkeyCaptureDialog.ShowDialog(_context.HostForm, currentHotkey);
            return Serialize(result);
        });
    }

    public string ApplyPttHotkey(string hotkey)
    {
        return RunOnUiForJson(() => ApplyPttHotkeyCore(hotkey));
    }

    private string ApplyPttHotkeyCore(string hotkey)
    {
        var trimmed = hotkey?.Trim() ?? string.Empty;
        if (!HotkeyParser.TryParse(trimmed, out _, out var parseError))
        {
            return Serialize(new ApplyPttHotkeyResultDto
            {
                Ok = false,
                ActiveHotkey = _context.Settings.PttHotkey,
                Error = parseError ?? "热键格式无效。",
            });
        }

        if (_context.SettingsApplyHost is null)
        {
            _context.Settings.PttHotkey = trimmed;
            _context.SettingsStore.Save(_context.Settings);
            return Serialize(new ApplyPttHotkeyResultDto
            {
                Ok = true,
                ActiveHotkey = trimmed,
                Error = "已写入 settings.json，但托盘未连接：请重启应用使热键生效。",
            });
        }

        if (!_context.SettingsApplyHost.TryUpdatePttHotkey(trimmed, out var registerError))
        {
            _context.SettingsApplyHost.NotifyPttHotkeyFailed(registerError);
            return Serialize(new ApplyPttHotkeyResultDto
            {
                Ok = false,
                ActiveHotkey = _context.SettingsApplyHost.PushToTalk.HotkeyDisplay,
                Error = registerError ?? "热键注册失败。",
            });
        }

        _context.Settings.PttHotkey = trimmed;
        _context.SettingsApplyHost.RegisteredPttHotkey = trimmed;
        _context.SettingsStore.Save(_context.Settings);
        _context.SettingsApplyHost.NotifyPttHotkeyUpdated(_context.SettingsApplyHost.PushToTalk.HotkeyDisplay);

        return Serialize(new ApplyPttHotkeyResultDto
        {
            Ok = true,
            ActiveHotkey = _context.SettingsApplyHost.PushToTalk.HotkeyDisplay,
        });
    }

    public string OpenFolderPickerDialog(string? initialPath)
    {
        return RunOnUiForJson(() =>
        {
            var result = FolderPickerDialog.Show(_context.HostForm, initialPath);
            return Serialize(result);
        });
    }

    private string SaveSettingsDraftCore(string draftJson)
    {
        if (!TryParseDraft(draftJson, out var draft, out var parseError))
        {
            return Serialize(new SaveSettingsResultDto { Ok = false, Error = parseError ?? "无效 JSON。" });
        }

        var validation = SettingsDraftValidator.Validate(draft!, _context.Settings);
        if (!validation.Ok)
        {
            var first = validation.Errors.FirstOrDefault();
            return Serialize(new SaveSettingsResultDto
            {
                Ok = false,
                Error = first?.Message ?? "设置校验失败。",
            });
        }

        var incoming = SettingsDraftMapper.ToAppSettings(draft!, _context.Settings);
        incoming.ApiBaseUrl = ApiUrlNormalizer.NormalizeBaseUrl(incoming.ApiBaseUrl);

        if (incoming.PromptRefineEnabled)
        {
            if (!PrivacyConsent.EnsureAccepted(incoming, incoming.ApiBaseUrl, _context.HostForm))
            {
                return Serialize(new SaveSettingsResultDto { Ok = false, Error = "隐私未确认，已取消保存。" });
            }

            if (PrivacyConfirmation.TryResolveHost(incoming.ApiBaseUrl, out var host)
                && (PrivacyConfirmation.IsLoopbackHost(host)
                    || !PrivacyConfirmation.ShouldPromptForHost(incoming.ApiBaseUrl, incoming.PrivacyAcceptedHost)))
            {
                incoming.PrivacyAcceptedHost = host;
            }
        }

        var previous = SettingsApplyService.CloneSnapshot(_context.Settings);
        var applyService = _context.SettingsApplyService ?? _settingsApplyService;

        if (_context.SettingsApplyHost is not null)
        {
            applyService.Apply(previous, incoming, _context.SettingsApplyHost);
            _context.RuntimeTriggerMode = _context.SettingsApplyHost.CurrentTriggerMode;
            _context.MasterEnabled = _context.Settings.MasterEnabled;
            return Serialize(new SaveSettingsResultDto { Ok = true });
        }

        // TODO(A1/integration): wire ISettingsApplyHost from TrayApplicationContext when opening settings Web UI.
        SettingsCopier.CopyInto(incoming, _context.Settings);
        _context.SettingsStore.Save(_context.Settings);
        return Serialize(new SaveSettingsResultDto
        {
            Ok = true,
            Warning = "SettingsApplyHost 未配置：已写入 settings.json，但未应用 pipeline/热键/唤醒等运行时变更。",
        });
    }

    private static bool TryParseDraft(string draftJson, out SettingsDraftDto? draft, out string? error)
    {
        draft = null;
        error = null;
        if (string.IsNullOrWhiteSpace(draftJson))
        {
            error = "draftJson 为空。";
            return false;
        }

        try
        {
            draft = JsonSerializer.Deserialize<SettingsDraftDto>(draftJson, JsonOptions);
            if (draft is null)
            {
                error = "无法解析设置 draft。";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
