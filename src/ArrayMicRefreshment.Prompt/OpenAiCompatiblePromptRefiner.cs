using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Prompt;

public sealed class OpenAiCompatiblePromptRefiner : IPromptRefiner
{
    private readonly AppSettings _settings;
    private readonly SkillsCatalog _catalog;
    private readonly OpenAiChatClient _client;

    public OpenAiCompatiblePromptRefiner(AppSettings settings, SkillsCatalog catalog, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _catalog = catalog;
        _client = new OpenAiChatClient(settings, handler);
    }

    public bool IsEnabled => _settings.PromptRefineEnabled;

    public async Task<string> RefineAsync(string raw, PromptIntent intent, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return raw;
        }

        var specialistKey = SpecialistKeyMapper.ToSpecialistKey(intent);
        if (!_catalog.Manifest.Specialists.TryGetValue(specialistKey, out var specialist))
        {
            specialist = _catalog.Manifest.Specialists["general-ai"];
        }

        var stackBody = _catalog.ResolveStackContent(specialist.Stack);
        var overlay = _catalog.TryResolveOptionalOverlay(_settings.OptionalOverlaySkills);
        var systemPrompt = string.IsNullOrEmpty(overlay)
            ? stackBody
            : overlay + "\n\n---\n\n" + stackBody;

        var messages = new List<(string Role, string Content)>
        {
            ("system", systemPrompt),
            ("user", raw),
        };

        var refined = await _client.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
        return refined.Trim();
    }
}
