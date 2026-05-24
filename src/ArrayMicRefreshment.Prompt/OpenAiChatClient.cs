using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Prompt;

public sealed class OpenAiChatClient
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;

    public OpenAiChatClient(AppSettings settings, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _settings = settings;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    public async Task<string> CompleteAsync(
        IReadOnlyList<(string Role, string Content)> messages,
        CancellationToken cancellationToken)
    {
        var baseUrl = ApiUrlNormalizer.NormalizeBaseUrl(_settings.ApiBaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new RefineApiException("API Base URL 未填写。请在设置中填写后再试。");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new RefineApiException($"API Base URL 无效: {baseUrl}");
        }

        var url = $"{baseUrl.TrimEnd('/')}/chat/completions";

        Log.Information(
            "[DIAGNOSTIC] OpenAiChatClient request URL={Url}, Model={Model}, Messages={Count}, " +
            "ApiKeySet={KeySet}, ApiKeyLength={KeyLen}, ApiKeyPrefix={KeyPrefix}",
            url, _settings.ApiModel, messages.Count,
            !string.IsNullOrWhiteSpace(_settings.ApiKey),
            _settings.ApiKey?.Length ?? 0,
            string.IsNullOrWhiteSpace(_settings.ApiKey) ? "(none)" : _settings.ApiKey[..Math.Min(8, _settings.ApiKey.Length)] + "…");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        var body = new
        {
            model = _settings.ApiModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
        };
        var jsonBody = JsonSerializer.Serialize(body);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        Log.Debug("[DIAGNOSTIC] Request body length={Len}, firstMsgRole={Role}, firstMsgContentLen={ContentLen}",
            jsonBody.Length,
            messages.Count > 0 ? messages[0].Role : "(none)",
            messages.Count > 0 ? messages[0].Content?.Length ?? 0 : 0);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Error(ex, "[DIAGNOSTIC] OpenAiChatClient: request timed out after {Timeout}s", _http.Timeout.TotalSeconds);
            throw new RefineApiException("Prompt refine API request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "[DIAGNOSTIC] OpenAiChatClient: request failed. Inner={Inner}, Status={Status}",
                ex.InnerException?.GetType().Name, ex.StatusCode);
            throw new RefineApiException($"Prompt refine API request failed: {ex.Message}", ex);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("[DIAGNOSTIC] OpenAiChatClient response status={Status}, bodyLength={Length}, body={Body}",
            (int)response.StatusCode, payload?.Length ?? 0, Truncate(payload ?? "(empty)", 500));

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log.Error("[DIAGNOSTIC] OpenAiChatClient: 401 Unauthorized — API Key 无效或未提供");
            throw new RefineApiException("Prompt refine API returned 401 Unauthorized. Check API key.");
        }

        if (response.StatusCode == (HttpStatusCode)429)
        {
            Log.Error("[DIAGNOSTIC] OpenAiChatClient: 429 Too Many Requests");
            throw new RefineApiException("Prompt refine API returned 429 Too Many Requests.");
        }

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("[DIAGNOSTIC] OpenAiChatClient: error response status={Status}, body={Payload}",
                (int)response.StatusCode, Truncate(payload, 400));
            throw new RefineApiException(
                $"Prompt refine API returned {(int)response.StatusCode}: {Truncate(payload, 400)}");
        }

        var content = ExtractAssistantContent(payload);
        Log.Debug("[DIAGNOSTIC] OpenAiChatClient: assistant content length={Length}", content?.Length ?? 0);
        return content;
    }

    internal static string ExtractAssistantContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return content ?? string.Empty;
    }

    internal static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
