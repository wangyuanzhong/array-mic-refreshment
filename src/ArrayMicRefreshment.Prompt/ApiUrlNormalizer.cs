namespace ArrayMicRefreshment.Prompt;

/// <summary>Normalize OpenAI-compatible API base URLs (many providers require a /v1 suffix).</summary>
public static class ApiUrlNormalizer
{
    public static string NormalizeBaseUrl(string apiBaseUrl)
    {
        var trimmed = apiBaseUrl.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return trimmed;
        }

        trimmed = trimmed.TrimEnd('/');

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Host.Contains("deepseek.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("openai.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("moonshot.cn", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed + "/v1";
        }

        if (trimmed.Contains("11434", StringComparison.Ordinal)
            || uri?.Host is "localhost" or "127.0.0.1")
        {
            return trimmed + "/v1";
        }

        return trimmed;
    }
}
