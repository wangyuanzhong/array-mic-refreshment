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
    private const string DefaultPolishPrompt =
        "你是一位语音转写文本整理专家。你的唯一任务是将用户通过语音识别输入的口语化文本，整理成通顺、规范、适合直接使用的书面语。\n\n" +
        "整理规则（按优先级执行）：\n" +
        "1. 去除所有口误、重复词、口头禅和语气词（例如：嗯、啊、呃、那个、就是、然后然后）。\n" +
        "2. 修正语法错误，调整语序，使句子通顺流畅。\n" +
        "3. 添加适当的标点符号（逗号、句号、顿号、问号、感叹号等）。\n" +
        "4. 保持原文的核心意思和信息完整性，严禁添加原文没有的新内容，严禁删减原文包含的重要信息。\n" +
        "5. 将过长或结构混乱的句子适当分段，提高可读性。\n" +
        "6. 如果原文包含代码片段、技术术语、专有名词、人名地名，保持其原样不变，不要翻译或改写。\n" +
        "7. 去除语音中常见的自我修正（例如：\"不对，我是说……\"），直接保留最终表达的意思。\n\n" +
        "【极其重要】你只输出整理后的纯文本内容。不要回答用户的问题，不要提供解释、建议、评论或任何额外内容。不要加引号包裹输出。";

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

        var messages = new List<(string Role, string Content)>
        {
            ("system", systemPrompt),
            ("user", raw),
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
