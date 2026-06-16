using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Prompt;

public sealed class OpenAiCompatiblePromptRefiner : IPromptRefiner
{
    private readonly AppSettings _settings;
    private readonly SkillsCatalog _catalog;
    private readonly OpenAiChatClient _client;

    /// <summary>Qwen3 hybrid models: per-turn soft switch to skip reasoning (official).</summary>
    private const string QwenNoThinkSuffix = "\n/no_think";

    public OpenAiCompatiblePromptRefiner(AppSettings settings, SkillsCatalog catalog, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _catalog = catalog;
        _client = new OpenAiChatClient(settings, handler);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings.PromptRefineEnabled = settings.PromptRefineEnabled;
        _settings.ForcedIntent = settings.ForcedIntent;
        _settings.OnRefineFailure = settings.OnRefineFailure;

        var preset = settings.CurrentPreset;
        _settings.ApiBaseUrl = preset.ApiBaseUrl;
        _settings.ApiKey = preset.ApiKey;
        _settings.ApiModel = preset.ApiModel;

        Log.Debug(
            "PromptRefiner: settings applied. IsEnabled={IsEnabled}, Preset={Preset}, Url={Url}, Model={Model}",
            IsEnabled, preset.Name, preset.ApiBaseUrl, preset.ApiModel);
    }

    public bool IsEnabled => _settings.PromptRefineEnabled;

    public async Task<string> RefineAsync(string raw, PromptIntent intent, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return raw;
        }

        var plainTextPath = TryResolvePlainTextRefine(raw, intent, out var systemPrompt, out var userContent, out var englishTokens);
        if (!plainTextPath)
        {
            systemPrompt = BuildSpecialistSystemPrompt(intent);
            userContent = raw;
        }

        Log.Debug(
            "PromptRefiner systemPromptLength={SysLen}, rawLength={RawLen}, mixedPlainText={Mixed}",
            systemPrompt?.Length ?? 0,
            raw?.Length ?? 0,
            plainTextPath && MixedTranscriptRefineGuard.LooksMixed(raw));

        var messages = new List<(string Role, string Content)>
        {
            ("system", systemPrompt),
            ("user", userContent),
        };

        var refined = await _client.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
        var trimmed = refined?.Trim() ?? string.Empty;
        Log.Information("PromptRefiner completed. Input={RawLen} chars, Output={RefinedLen} chars (trimmed={TrimmedLen})",
            raw?.Length ?? 0, refined?.Length ?? 0, trimmed.Length);

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            Log.Warning("PromptRefiner: LLM returned empty/whitespace. System prompt length was {SysLen}. " +
                "Possible causes: context length exceeded, unsupported prompt format, or API returned empty content.",
                systemPrompt?.Length ?? 0);
            return trimmed;
        }

        if (plainTextPath
            && MixedTranscriptRefineGuard.LooksMixed(raw)
            && !MixedTranscriptRefineGuard.PassesValidation(raw, trimmed, englishTokens!))
        {
            Log.Warning(
                "Mixed-transcript refine altered protected English tokens; using ASR raw ({RawLen} chars).",
                raw?.Length ?? 0);
            return raw.Trim();
        }

        return trimmed;
    }

    private bool TryResolvePlainTextRefine(
        string raw,
        PromptIntent intent,
        out string systemPrompt,
        out string userContent,
        out IReadOnlyList<string> englishTokens)
    {
        englishTokens = Array.Empty<string>();
        var specialistKey = ForcedStyleSelection.GetEffectiveKey(_settings);
        if (string.Equals(specialistKey, ForcedStyleSelection.AutoKey, StringComparison.OrdinalIgnoreCase))
        {
            specialistKey = SpecialistKeyMapper.ToSpecialistKey(intent);
        }

        if (!string.Equals(specialistKey, "plain-text", StringComparison.OrdinalIgnoreCase)
            && intent != PromptIntent.PlainText)
        {
            systemPrompt = string.Empty;
            userContent = raw;
            return false;
        }

        var mixed = MixedTranscriptRefineGuard.LooksMixed(raw);
        systemPrompt = mixed ? PlainTextPolishPrompts.Mixed : PlainTextPolishPrompts.ChineseOnly;
        userContent = raw.TrimEnd();
        if (mixed)
        {
            englishTokens = MixedTranscriptRefineGuard.ExtractEnglishTokens(raw);
            userContent = MixedTranscriptRefineGuard.AppendProtectionHint(userContent, englishTokens);
            Log.Debug(
                "Using built-in mixed plain-text polish prompt ({TokenCount} protected tokens)",
                englishTokens.Count);
        }
        else
        {
            Log.Debug("Using built-in plain-text polish prompt");
        }

        userContent += QwenNoThinkSuffix;
        return true;
    }

    private string BuildSpecialistSystemPrompt(PromptIntent intent)
    {
        var specialistKey = ForcedStyleSelection.GetEffectiveKey(_settings);
        if (string.Equals(specialistKey, ForcedStyleSelection.AutoKey, StringComparison.OrdinalIgnoreCase))
        {
            specialistKey = SpecialistKeyMapper.ToSpecialistKey(intent);
        }

        if (_catalog is not null
            && RefinementStyleService.TryBuildSystemPrompt(
                _catalog,
                specialistKey,
                _settings.OptionalOverlaySkills,
                out var skillPrompt)
            && !string.IsNullOrWhiteSpace(skillPrompt)
            && skillPrompt.Length > 50)
        {
            Log.Debug("Using skill-based system prompt for specialist={Specialist}", specialistKey);
            return skillPrompt;
        }

        Log.Debug("Skill stack empty or missing; falling back to built-in plain-text polish");
        return PlainTextPolishPrompts.ChineseOnly;
    }
}
