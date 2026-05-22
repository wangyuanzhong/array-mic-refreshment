namespace ArrayMicRefreshment.Core;

/// <summary>Privacy prompt rules for remote prompt-refine API hosts.</summary>
public static class PrivacyConfirmation
{
    public static bool ShouldPromptForHost(string apiBaseUrl, string? acceptedHost)
    {
        if (!TryResolveHost(apiBaseUrl, out var host))
        {
            return false;
        }

        if (IsLoopbackHost(host))
        {
            return false;
        }

        return !string.Equals(acceptedHost, host, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryResolveHost(string apiBaseUrl, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(apiBaseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        if (host.StartsWith('[') && host.EndsWith(']') && host.Length >= 2)
        {
            host = host[1..^1];
        }

        return true;
    }

    public static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
