using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Validates settings drafts (mirrors legacy WinForms save rules without UI).</summary>
public static class SettingsDraftValidator
{
    public static SettingsValidationResultDto Validate(SettingsDraftDto draft, AppSettings template)
    {
        var errors = new List<SettingsValidationErrorDto>();

        if (!HotkeyParser.TryParse(draft.PttHotkey, out _, out var hotkeyError))
        {
            errors.Add(new SettingsValidationErrorDto
            {
                Field = "pttHotkey",
                Message = hotkeyError ?? "PTT 热键无效。",
            });
        }

        if (draft.TriggerMode == VoiceTriggerMode.WakeWordOnly
            && string.IsNullOrWhiteSpace(draft.WakeWordPhrase))
        {
            errors.Add(new SettingsValidationErrorDto
            {
                Field = "wakeWordPhrase",
                Message = "已选择「唤醒词」触发模式，请填写唤醒词文本。",
            });
        }

        if (!string.IsNullOrWhiteSpace(draft.WakeWordPhrase))
        {
            var modelsDir = string.IsNullOrWhiteSpace(draft.ModelsDirectory)
                ? template.ModelsDirectory
                : draft.ModelsDirectory;
            var sensitivity = draft.WakeWordSensitivity;
            if (!WakeWordPhraseEncoding.CanEncode(
                    modelsDir,
                    draft.WakeWordPhrase,
                    sensitivity,
                    out var encodeError)
                && encodeError is not null)
            {
                errors.Add(new SettingsValidationErrorDto
                {
                    Field = "wakeWordPhrase",
                    Message = encodeError,
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(draft.SelectedAsrModelId))
        {
            var available = SenseVoiceModelResolver.ListAvailableModels(
                string.IsNullOrWhiteSpace(draft.ModelsDirectory)
                    ? template.ModelsDirectory
                    : draft.ModelsDirectory);
            var isInstalled = available.Any(a => a.Id == draft.SelectedAsrModelId);
            if (!isInstalled)
            {
                var displayName = AsrModelInfo.All
                    .FirstOrDefault(m => m.Id == draft.SelectedAsrModelId)?.DisplayName
                    ?? draft.SelectedAsrModelId;
                errors.Add(new SettingsValidationErrorDto
                {
                    Field = "selectedAsrModelId",
                    Message = $"ASR 模型「{displayName}」尚未安装。请先下载或切换已安装模型。",
                });
            }
        }

        var apiBaseUrl = NormalizeActiveApiBaseUrl(draft);

        if (draft.PromptRefineEnabled)
        {
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                errors.Add(new SettingsValidationErrorDto
                {
                    Field = "apiBaseUrl",
                    Message = "已启用「提示词整理」，但未填写 API Base URL。",
                });
            }
            else
            {
                var isLoopback = PrivacyConfirmation.TryResolveHost(apiBaseUrl, out var apiHost)
                    && PrivacyConfirmation.IsLoopbackHost(apiHost);
                var apiKey = GetActiveApiKey(draft);
                if (string.IsNullOrWhiteSpace(apiKey) && !isLoopback)
                {
                    errors.Add(new SettingsValidationErrorDto
                    {
                        Field = "apiKey",
                        Message = "已启用「提示词整理」，但未填写 API Key（本机 Ollama 地址除外）。",
                    });
                }
            }
        }

        try
        {
            var skillsDir = SkillsPathResolver.Resolve(draft.SkillsDirectory?.Trim() ?? template.SkillsDirectory);
            var catalog = SkillsCatalog.Load(skillsDir);
            if (catalog.MissingFiles.Count > 0)
            {
                errors.Add(new SettingsValidationErrorDto
                {
                    Field = "skillsDirectory",
                    Message = $"缺少 skill 文件: {string.Join(", ", catalog.MissingFiles)}",
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add(new SettingsValidationErrorDto
            {
                Field = "skillsDirectory",
                Message = $"Skills 目录错误: {ex.Message}",
            });
        }

        if (draft.FeaturePresets is { Count: > 0 })
        {
            var llmNames = new HashSet<string>(
                (draft.LlmPresets ?? [])
                    .Select(p => p.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < draft.FeaturePresets.Count; i++)
            {
                var fp = draft.FeaturePresets[i];
                if (string.IsNullOrWhiteSpace(fp.Name))
                {
                    errors.Add(new SettingsValidationErrorDto
                    {
                        Field = $"featurePresets[{i}].name",
                        Message = "功能预设名称不能为空。",
                    });
                }

                if (!string.IsNullOrWhiteSpace(fp.LlmPresetName)
                    && !llmNames.Contains(fp.LlmPresetName))
                {
                    errors.Add(new SettingsValidationErrorDto
                    {
                        Field = $"featurePresets[{i}].llmPresetName",
                        Message = $"功能预设「{fp.Name}」引用的 LLM 预设「{fp.LlmPresetName}」不存在。",
                    });
                }
            }

            if (draft.SelectedFeaturePresetIndex < 0
                || draft.SelectedFeaturePresetIndex >= draft.FeaturePresets.Count)
            {
                errors.Add(new SettingsValidationErrorDto
                {
                    Field = "selectedFeaturePresetIndex",
                    Message = "所选功能预设索引无效。",
                });
            }
        }

        return new SettingsValidationResultDto
        {
            Ok = errors.Count == 0,
            Errors = errors,
        };
    }

    private static string NormalizeActiveApiBaseUrl(SettingsDraftDto draft)
    {
        if (draft.LlmPresets is null || draft.LlmPresets.Count == 0)
        {
            return string.Empty;
        }

        var idx = Math.Clamp(draft.SelectedLlmPresetIndex, 0, draft.LlmPresets.Count - 1);
        var normalized = ApiUrlNormalizer.NormalizeBaseUrl(draft.LlmPresets[idx].ApiBaseUrl ?? string.Empty);
        draft.LlmPresets[idx].ApiBaseUrl = normalized;
        return normalized;
    }

    private static string GetActiveApiKey(SettingsDraftDto draft)
    {
        if (draft.LlmPresets is null || draft.LlmPresets.Count == 0)
        {
            return string.Empty;
        }

        var idx = Math.Clamp(draft.SelectedLlmPresetIndex, 0, draft.LlmPresets.Count - 1);
        return draft.LlmPresets[idx].ApiKey ?? string.Empty;
    }
}
