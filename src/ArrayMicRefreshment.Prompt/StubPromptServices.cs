using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Prompt;

/// <summary>Phase 4: manifest.yaml router + upstream prompt stack.</summary>
public sealed class StubIntentRouter : IIntentRouter
{
    public Task<(PromptIntent Intent, float Confidence)> RouteAsync(string raw, CancellationToken cancellationToken) =>
        Task.FromResult((PromptIntent.GeneralAi, 1f));

    public void ApplySettings(AppSettings settings) { }
}

public sealed class StubPromptRefiner : IPromptRefiner
{
    private readonly bool _enabled;

    public StubPromptRefiner(bool enabled) => _enabled = enabled;

    public bool IsEnabled => _enabled;

    public Task<string> RefineAsync(string raw, PromptIntent intent, CancellationToken cancellationToken) =>
        Task.FromResult($"[refined:{intent}] {raw}");

    public void ApplySettings(AppSettings settings) { }
}
