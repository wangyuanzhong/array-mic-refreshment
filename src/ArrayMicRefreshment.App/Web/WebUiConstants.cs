namespace ArrayMicRefreshment.App.Web;

internal static class WebUiConstants
{
    public const string WwwRootVirtualHost = "amr.local";

    public const string WwwRootBaseUrl = "https://amr.local/";

    public static string HashUrl(string hashRoute) =>
        $"{WwwRootBaseUrl}index.html{NormalizeHashRoute(hashRoute)}";

    private static string NormalizeHashRoute(string hashRoute)
    {
        if (string.IsNullOrWhiteSpace(hashRoute))
        {
            return "#/settings";
        }

        return hashRoute.StartsWith('#') ? hashRoute : "#" + hashRoute.TrimStart('/');
    }
}
