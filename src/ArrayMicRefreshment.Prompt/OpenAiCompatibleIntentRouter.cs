using ArrayMicRefreshment.Core;
using Serilog;

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

    public void ApplySettings(AppSettings settings)
    {
        _settings.ForcedIntent = settings.ForcedIntent;

        var preset = settings.CurrentPreset;
        _settings.ApiBaseUrl = preset.ApiBaseUrl;
        _settings.ApiKey = preset.ApiKey;
        _settings.ApiModel = preset.ApiModel;

        Log.Debug(
            "IntentRouter: settings applied. ForcedIntent={Intent}, Preset={Preset}, Url={Url}",
            _settings.ForcedIntent, preset.Name, preset.ApiBaseUrl);
    }

    public async Task<(PromptIntent Intent, float Confidence)> RouteAsync(string raw, CancellationToken cancellationToken)
    {
        // Fast-path: if the user has chosen a forced intent in settings, skip the
        // expensive LLM round-trip entirely.
        if (_settings.ForcedIntent != PromptIntent.Auto)
        {
            Log.Debug("IntentRouter: forced intent={Intent}, skipping LLM routing", _settings.ForcedIntent);
            return (_settings.ForcedIntent, 1f);
        }

        Log.Debug("IntentRouter: routing raw text ({Length} chars)", raw?.Length ?? 0);

        string systemPrompt;
        try
        {
            systemPrompt = _catalog.RequireRouterSystemPrompt();
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "IntentRouter: skills/router prompt unavailable; using GeneralAi without LLM routing.");
            return (PromptIntent.GeneralAi, 0f);
        }

        Log.Debug("IntentRouter: system prompt length={Length}", systemPrompt?.Length ?? 0);

        var messages = new List<(string Role, string Content)>
        {
            ("system", systemPrompt),
            ("user", raw),
        };

        var content = await _client.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
        Log.Debug("IntentRouter: LLM response={Response}", content);

        if (!IntentResponseParser.TryParseIntentJson(content, out var upstreamIntent, out var confidence))
        {
            Log.Warning("IntentRouter: failed to parse LLM response as intent JSON. Response: {Response}", content);
            return (PromptIntent.GeneralAi, 0f);
        }

        var map = _catalog.Manifest.Router.IntentMap;
        if (!map.ContainsKey(upstreamIntent!.Trim()))
        {
            Log.Warning("IntentRouter: upstream intent '{Intent}' not in map. Available: {Keys}", upstreamIntent, string.Join(", ", map.Keys));
            return (PromptIntent.GeneralAi, 0f);
        }

        var intent = SpecialistKeyMapper.FromUpstreamIntent(upstreamIntent, map);
        Log.Information("IntentRouter: resolved intent={Intent} (confidence={Confidence:F2})", intent, confidence);
        return (intent, confidence);
    }
}
