namespace Highlightify.Application;

public sealed record AppOptions(
    IReadOnlyList<string> HighlightSources,
    string? SpotifyClientId,
    string PlaylistName,
    string? PlaylistId,
    string? InstagramCookieHeader,
    string? InstagramCookiesFromBrowser,
    string? InstagramCookiePath,
    string RedirectUri,
    bool OpenBrowser,
    bool DryRun)
{
    public static AppOptions Parse(string[] args)
    {
        var highlightSources = new List<string>();
        var spotifyClientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
        var playlistName = Environment.GetEnvironmentVariable("HIGHLIGHTIFY_PLAYLIST_NAME");
        var playlistId = Environment.GetEnvironmentVariable("HIGHLIGHTIFY_PLAYLIST_ID");
        var instagramCookieHeader = Environment.GetEnvironmentVariable("INSTAGRAM_COOKIE");
        var instagramCookieFile = Environment.GetEnvironmentVariable("INSTAGRAM_COOKIE_FILE");
        var instagramCookiesFromBrowser = Environment.GetEnvironmentVariable("INSTAGRAM_COOKIES_FROM_BROWSER");
        var instagramCookiePath = Environment.GetEnvironmentVariable("INSTAGRAM_COOKIE_PATH");
        var redirectUri = Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI")
                          ?? "http://127.0.0.1:54321/callback/";
        var openBrowser = true;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--highlight":
                    highlightSources.Add(NextValue());
                    break;
                case "--html":
                    highlightSources.Add(NextValue());
                    break;
                case "--highlight-file":
                    {
                        var path = NextValue();
                        highlightSources.AddRange(File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)));
                        break;
                    }
                case "--spotify-client-id":
                    spotifyClientId = NextValue();
                    break;
                case "--playlist-name":
                    playlistName = NextValue();
                    break;
                case "--playlist-id":
                    playlistId = NextValue();
                    break;
                case "--instagram-cookie":
                    instagramCookieHeader = NextValue();
                    break;
                case "--instagram-cookie-file":
                    instagramCookieFile = NextValue();
                    break;
                case "--instagram-cookies-from-browser":
                    instagramCookiesFromBrowser = NextValue();
                    break;
                case "--instagram-cookie-path":
                    instagramCookiePath = NextValue();
                    break;
                case "--redirect-uri":
                    redirectUri = NextValue();
                    break;
                case "--no-browser":
                    openBrowser = false;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }

            continue;

            string NextValue()
            {
	            if (i + 1 >= args.Length)
		            throw new ArgumentException($"Missing value for {arg}");

	            i++;
	            return args[i];
            }
        }

        if (!string.IsNullOrWhiteSpace(instagramCookieFile) && string.IsNullOrWhiteSpace(instagramCookieHeader))
            instagramCookieHeader = CookieFileReader.ReadAsCookieHeader(instagramCookieFile!);

        if (!string.IsNullOrWhiteSpace(instagramCookiePath) &&
            string.IsNullOrWhiteSpace(instagramCookiesFromBrowser))
        {
            instagramCookiesFromBrowser = BrowserCookieLoader.InferFirefoxBrowserSpec(instagramCookiePath);
            if (string.IsNullOrWhiteSpace(instagramCookiesFromBrowser) &&
                string.IsNullOrWhiteSpace(instagramCookieHeader))
                instagramCookieHeader = CookieFileReader.ReadAsCookieHeader(instagramCookiePath!);
        }

        if (!string.IsNullOrWhiteSpace(instagramCookiesFromBrowser))
            instagramCookieHeader = null;

        return new AppOptions(
            highlightSources,
            spotifyClientId,
            playlistName ?? "Instagram Highlights",
            playlistId,
            instagramCookieHeader,
            instagramCookiesFromBrowser,
            instagramCookiePath,
            redirectUri,
            openBrowser,
            dryRun);
    }

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(SpotifyClientId))
        {
            error = "Missing --spotify-client-id or SPOTIFY_CLIENT_ID.";
            return false;
        }

        if (HighlightSources.Count == 0)
        {
            error = "Provide at least one --highlight, --highlight-file, or --html input.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}