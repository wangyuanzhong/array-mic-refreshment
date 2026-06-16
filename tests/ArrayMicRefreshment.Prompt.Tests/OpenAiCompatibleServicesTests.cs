using System.Net;
using System.Text;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.Prompt.Tests;

public class OpenAiCompatibleServicesTests
{
    private static SkillsCatalog LoadCatalog() =>
        SkillsCatalog.Load(SkillsPathResolver.Resolve("skills"));

    private static AppSettings TestSettings() => new()
    {
        ApiBaseUrl = "https://api.example.com/v1",
        ApiKey = "test-key",
        ApiModel = "test-model",
        PromptRefineEnabled = true,
        ForcedIntent = PromptIntent.Auto,
    };

    [Fact]
    public async Task IntentRouter_success_parses_intent()
    {
        var response = ChatCompletionJson("""{"intent":"write_code","confidence":0.91}""");
        var router = CreateRouter(response, out _);
        var (intent, confidence) = await router.RouteAsync("build api service", CancellationToken.None);
        Assert.Equal(PromptIntent.CodeEditing, intent);
        Assert.Equal(0.91f, confidence, 2);
    }

    [Fact]
    public async Task IntentRouter_non_json_falls_back_to_GeneralAi_zero_confidence()
    {
        var response = ChatCompletionJson("not json at all");
        var router = CreateRouter(response, out _);
        var (intent, confidence) = await router.RouteAsync("hello", CancellationToken.None);
        Assert.Equal(PromptIntent.GeneralAi, intent);
        Assert.Equal(0f, confidence);
    }

    [Fact]
    public async Task IntentRouter_unknown_intent_falls_back_GeneralAi_zero_confidence()
    {
        var response = ChatCompletionJson("""{"intent":"unknown_thing","confidence":0.5}""");
        var router = CreateRouter(response, out _);
        var (intent, confidence) = await router.RouteAsync("hello", CancellationToken.None);
        Assert.Equal(PromptIntent.GeneralAi, intent);
        Assert.Equal(0f, confidence);
    }

    [Fact]
    public async Task IntentRouter_401_throws_RefineApiException()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var router = new OpenAiCompatibleIntentRouter(TestSettings(), LoadCatalog(), handler);
        await Assert.ThrowsAsync<RefineApiException>(() =>
            router.RouteAsync("ping", CancellationToken.None));
    }

    [Fact]
    public async Task IntentRouter_timeout_throws_RefineApiException()
    {
        var handler = new StubHandler(_ =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("timed out")));
        var router = new OpenAiCompatibleIntentRouter(TestSettings(), LoadCatalog(), handler);
        await Assert.ThrowsAsync<RefineApiException>(() =>
            router.RouteAsync("ping", CancellationToken.None));
    }

    [Fact]
    public async Task PromptRefiner_success_returns_content()
    {
        var response = ChatCompletionJson("refined output");
        var refiner = CreateRefiner(response, out _);
        var text = await refiner.RefineAsync("raw words", PromptIntent.GeneralAi, CancellationToken.None);
        Assert.Equal("refined output", text);
    }

    [Fact]
    public async Task PromptRefiner_429_throws_RefineApiException()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)));
        var refiner = new OpenAiCompatiblePromptRefiner(TestSettings(), LoadCatalog(), handler);
        await Assert.ThrowsAsync<RefineApiException>(() =>
            refiner.RefineAsync("raw", PromptIntent.GeneralAi, CancellationToken.None));
    }

    [Fact]
    public async Task PromptRefiner_plain_text_mixed_uses_mixed_prompt_and_falls_back_when_english_lost()
    {
        var settings = TestSettings();
        settings.ForcedIntent = PromptIntent.PlainText;
        string? capturedBody = null;
        var handler = new StubHandler(async req =>
        {
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ChatCompletionJson("把部署到预发环境"),
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var refiner = new OpenAiCompatiblePromptRefiner(settings, LoadCatalog(), handler);
        var raw = "嗯把 deploy 到 staging";
        var result = await refiner.RefineAsync(raw, PromptIntent.PlainText, CancellationToken.None);
        Assert.Equal(raw.Trim(), result);
        Assert.NotNull(capturedBody);
        Assert.Contains("NEVER translate between Chinese and English", capturedBody);
        Assert.Contains("deploy", capturedBody);
    }

    [Fact]
    public async Task PromptRefiner_plain_text_chinese_only_uses_default_prompt()
    {
        var settings = TestSettings();
        settings.ForcedIntent = PromptIntent.PlainText;
        string? capturedBody = null;
        var handler = new StubHandler(async req =>
        {
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ChatCompletionJson("今天天气不错。"),
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var refiner = new OpenAiCompatiblePromptRefiner(settings, LoadCatalog(), handler);
        var result = await refiner.RefineAsync("嗯今天天气不错", PromptIntent.PlainText, CancellationToken.None);
        Assert.Equal("今天天气不错。", result);
        Assert.NotNull(capturedBody);
        Assert.Contains("You clean speech-to-text transcripts.", capturedBody);
        Assert.DoesNotContain("NEVER translate between Chinese and English", capturedBody);
    }

    private static OpenAiCompatibleIntentRouter CreateRouter(string completionBody, out StubHandler handler)
    {
        handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(completionBody, Encoding.UTF8, "application/json"),
        }));
        return new OpenAiCompatibleIntentRouter(TestSettings(), LoadCatalog(), handler);
    }

    private static OpenAiCompatiblePromptRefiner CreateRefiner(string completionBody, out StubHandler handler)
    {
        handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(completionBody, Encoding.UTF8, "application/json"),
        }));
        return new OpenAiCompatiblePromptRefiner(TestSettings(), LoadCatalog(), handler);
    }

    private static string ChatCompletionJson(string assistantContent) =>
        $$"""
        {
          "choices": [
            { "message": { "role": "assistant", "content": {{System.Text.Json.JsonSerializer.Serialize(assistantContent)}} } }
          ]
        }
        """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _factory;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> factory) => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _factory(request);
    }
}
