using System.Net;
using System.Text;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App.Tests;

public class LlmConnectionTesterTests
{
    [Fact]
    public async Task TestAsync_success_returns_router_confidence()
    {
        var call = 0;
        var handler = new StubHandler(_ =>
        {
            call++;
            var content = call == 1
                ? """{"intent":"general_chat","confidence":0.88}"""
                : "refined output";
            var body = ChatCompletionJson(content);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        });

        var settings = new AppSettings
        {
            PromptRefineEnabled = true,
            ForcedIntent = PromptIntent.Auto,
            ApiBaseUrl = "https://api.example.com/v1",
            ApiKey = "key",
            ApiModel = "test-model",
            SkillsDirectory = "skills",
        };
        settings.MigrateLegacyApiSettings();

        var result = await LlmConnectionTester.TestAsync(settings, handler);
        Assert.True(result.Ok);
        Assert.Contains("成功", result.Message);
        Assert.Equal(0.88f, result.RouterConfidence, 2);
    }

    [Fact]
    public async Task TestAsync_missing_api_url_fails_fast()
    {
        var settings = new AppSettings
        {
            PromptRefineEnabled = true,
            ApiBaseUrl = string.Empty,
            LlmPresets =
            [
                new() { Name = "预设1", ApiBaseUrl = string.Empty, ApiKey = string.Empty, ApiModel = string.Empty },
                new() { Name = "预设2" },
                new() { Name = "预设3" },
            ],
        };

        var result = await LlmConnectionTester.TestAsync(settings);
        Assert.False(result.Ok);
        Assert.Contains("API Base URL", result.Message);
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
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
