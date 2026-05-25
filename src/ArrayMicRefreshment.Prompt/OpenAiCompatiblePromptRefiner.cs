using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Prompt;

public sealed class OpenAiCompatiblePromptRefiner : IPromptRefiner
{
    private readonly AppSettings _settings;
    private readonly SkillsCatalog _catalog;
    private readonly OpenAiChatClient _client;

    /// <summary>
    /// Default text-polishing system prompt.  Instructs the LLM to act as a
    /// transcript cleaner — remove filler words, fix grammar, add punctuation —
    /// and *never* answer the user's question.
    /// </summary>
    /// <summary>
    /// Keep in sync with <c>skills/upstream/array-mic/plain-text-polish.md</c> (prompt-only; not program logic).
    /// </summary>
    private const string DefaultPolishPrompt =
        "You clean speech-to-text transcripts.\n\n" +
        "Rules:\n" +
        "- Remove fillers, repetitions, and false starts (e.g. 嗯/啊/那个/就是).\n" +
        "- Fix punctuation and obvious ASR/word errors; keep grammar natural.\n" +
        "- Preserve meaning, tone, language, names, numbers, and code—do not translate or add facts.\n" +
        "- For short input (one sentence or a few words), change only what is necessary—no headings, no bullet lists, no expansion.\n" +
        "- Do not reason, plan, or explain. Output the cleaned line directly.\n\n" +
        "Output ONLY the cleaned text. No quotes, labels, markdown, or commentary.\n\n" +
        "/no_think";

    /// <summary>Qwen3 hybrid models: per-turn soft switch to skip reasoning (official).</summary>
    private const string QwenNoThinkSuffix = "\n/no_think";

    public OpenAiCompatiblePromptRefiner(AppSettings settings, SkillsCatalog catalog, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _catalog = catalog;
        _client = new OpenAiChatClient(settings, handler);
    }

    /// <summary>
    /// Build the system prompt for the given intent/skill.
    ///
    /// - PlainText → built-in Chinese transcript-polish prompt (no skill files).
    /// - Other     → resolve the specialist stack from manifest.yaml.
    /// </summary>
    private string BuildSystemPrompt(PromptIntent intent)
    {
        // Fast path: plain-text polish uses the built-in prompt so the user
        // never gets an AI-prompt transformation when they just want clean text.
        if (intent == PromptIntent.PlainText)
        {
            Log.Debug("Using built-in plain-text polish prompt");
            return DefaultPolishPrompt;
        }

        var specialistKey = SpecialistKeyMapper.ToSpecialistKey(intent);
        if (_catalog?.Manifest?.Specialists != null &&
            _catalog.Manifest.Specialists.TryGetValue(specialistKey, out var specialist))
        {
            try
            {
                var stackBody = _catalog.ResolveStackContent(specialist.Stack);
                var overlay = _catalog.TryResolveOptionalOverlay(_settings.OptionalOverlaySkills);
                var skillPrompt = string.IsNullOrEmpty(overlay)
                    ? stackBody
                    : overlay + "\n\n---\n\n" + stackBody;

                if (!string.IsNullOrWhiteSpace(skillPrompt) && skillPrompt.Length > 50)
                {
                    Log.Debug("Using skill-based system prompt for specialist={Specialist}", specialistKey);
                    return skillPrompt;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to build skill-based prompt for {Specialist}; falling back to default.", specialistKey);
            }
        }

        Log.Debug("Skill stack empty or missing; falling back to built-in plain-text polish");
        return DefaultPolishPrompt;
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

        var systemPrompt = BuildSystemPrompt(intent);

        Log.Debug("PromptRefiner systemPromptLength={SysLen}, rawLength={RawLen}",
            systemPrompt?.Length ?? 0, raw?.Length ?? 0);

        // Qwen3 / Qwen3.5: /no_think on the user turn is the most reliable way to disable thinking
        // in LM Studio (see QwenLM/Qwen3 discussions). PlainText always appends it.
        var userContent = intent == PromptIntent.PlainText
            ? raw.TrimEnd() + QwenNoThinkSuffix
            : raw;

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
        }

        return trimmed;
    }
}
