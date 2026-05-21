using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArrayMicRefreshment.Core;

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
        var baseUrl = _settings.ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/chat/completions";

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
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RefineApiException("Prompt refine API request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new RefineApiException($"Prompt refine API request failed: {ex.Message}", ex);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new RefineApiException("Prompt refine API returned 401 Unauthorized. Check API key.");
        }

        if (response.StatusCode == (HttpStatusCode)429)
        {
            throw new RefineApiException("Prompt refine API returned 429 Too Many Requests.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new RefineApiException(
                $"Prompt refine API returned {(int)response.StatusCode}: {Truncate(payload, 400)}");
        }

        return ExtractAssistantContent(payload);
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
