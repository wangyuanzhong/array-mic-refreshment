using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Prompt;

public sealed class OpenAiCompatibleIntentRouter : IIntentRouter
{
    private readonly AppSettings _settings;
    private readonly SkillsCatalog _catalog;
    private readonly OpenAiChatClient _client;

    public OpenAiCompatibleIntentRouter(AppSettings settings, SkillsCatalog catalog, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _catalog = catalog;
        _client = new OpenAiChatClient(settings, handler);
    }

    public async Task<(PromptIntent Intent, float Confidence)> RouteAsync(string raw, CancellationToken cancellationToken)
    {
        var messages = new List<(string Role, string Content)>
        {
            ("system", _catalog.RequireRouterSystemPrompt()),
            ("user", raw),
        };

        var content = await _client.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
        if (!IntentResponseParser.TryParseIntentJson(content, out var upstreamIntent, out var confidence))
        {
            return (PromptIntent.GeneralAi, 0f);
        }

        var map = _catalog.Manifest.Router.IntentMap;
        if (!map.ContainsKey(upstreamIntent!.Trim()))
        {
            return (PromptIntent.GeneralAi, 0f);
        }

        var intent = SpecialistKeyMapper.FromUpstreamIntent(upstreamIntent, map);
        return (intent, confidence);
    }
}
