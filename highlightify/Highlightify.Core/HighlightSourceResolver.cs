namespace Highlightify.Core;

public static class HighlightSourceResolver
{
    public static string ResolveInstagramHighlightUrl(string highlightIdOrUrl)
    {
        if (Uri.TryCreate(highlightIdOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            return highlightIdOrUrl;

        return $"https://www.instagram.com/stories/highlights/{highlightIdOrUrl.Trim('/')}/";
    }
}