namespace Highlightify.Integrations;

public sealed class InstagramHighlightFetcher
{
    private static readonly Regex ScriptBlockRegex = new(@"<script\b[^>]*>(?<script>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PairFallbackRegex = new(
        "\"(?:artist_name|display_artist_name|artist)\"\\s*:\\s*\"(?<artist>[^\"]{1,200})\".{0,500}?\"(?:title|name|song_name|track_name)\"\\s*:\\s*\"(?<title>[^\"]{1,200})\"",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public InstagramHighlightFetcher()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    }

    public async Task<IReadOnlyList<TrackCandidate>> FetchCandidatesAsync(string highlightUrl, string? cookieHeader, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, highlightUrl);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
	        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
        {
            var location = response.Headers.Location?.ToString();
            throw new InvalidOperationException(
                $"Instagram redirected the highlight request with {((int)response.StatusCode)} {response.ReasonPhrase}."
                + (string.IsNullOrWhiteSpace(location) ? string.Empty : $" Location: {location}.")
                + " This usually means the session cookies are expired, incomplete, or the account requires a fresh login for this profile.");
        }

        if (!response.IsSuccessStatusCode)
	        throw new InvalidOperationException(
		        $"Instagram returned {((int)response.StatusCode)} {response.ReasonPhrase} for the highlight request.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractCandidates(html, highlightUrl);
    }

    public async Task<IReadOnlyList<TrackCandidate>> FetchCandidatesViaYtDlpAsync(
        string highlightUrl,
        string cookiesFromBrowser,
        CancellationToken cancellationToken = default)
    {
        var html = await FetchHtmlViaYtDlpAsync(highlightUrl, cookiesFromBrowser, cancellationToken);
        return ExtractCandidates(html, highlightUrl);
    }

    public IReadOnlyList<TrackCandidate> ExtractCandidates(string html, string sourceLabel)
    {
        var results = new List<TrackCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ScriptBlockRegex.Matches(html))
        {
            var script = WebUtility.HtmlDecode(match.Groups["script"].Value);
            foreach (var fragment in ExtractJsonFragments(script))
	            TryScanJson(fragment, sourceLabel, results, seen);
        }

        foreach (Match match in PairFallbackRegex.Matches(html))
	        AddCandidate(results, seen, match.Groups["title"].Value, match.Groups["artist"].Value, null, sourceLabel);

        return results;
    }

    private static async Task<string> FetchHtmlViaYtDlpAsync(
        string highlightUrl,
        string cookiesFromBrowser,
        CancellationToken cancellationToken)
    {
        var ytDlp = FindExecutable("yt-dlp");
        if (ytDlp is null)
	        throw new InvalidOperationException("yt-dlp was not found on PATH.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"highlightify-yt-dlp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlp,
                WorkingDirectory = tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add("--quiet");
            startInfo.ArgumentList.Add("--no-warnings");
            startInfo.ArgumentList.Add("--skip-download");
            startInfo.ArgumentList.Add("--write-pages");
            startInfo.ArgumentList.Add("--cookies-from-browser");
            startInfo.ArgumentList.Add(cookiesFromBrowser);
            startInfo.ArgumentList.Add(highlightUrl);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start yt-dlp.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"yt-dlp failed while loading the Instagram page: {stderr.Trim()}"
                    + (string.IsNullOrWhiteSpace(stdout) ? string.Empty : $" {stdout.Trim()}"));
            }

            var html = FindBestWrittenPage(tempDir);
            return html
                   ?? throw new InvalidOperationException("yt-dlp completed but did not write a readable Instagram page.");
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static string? FindBestWrittenPage(string root)
    {
        var candidates = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(file => file.Length > 0 && file.Length < 10 * 1024 * 1024)
            .OrderByDescending(file => file.Length)
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var file in candidates)
        {
            string text;
            try
            {
                text = File.ReadAllText(file.FullName);
            }
            catch
            {
                continue;
            }

            if (LooksLikeInstagramHtml(text))
                return text;
        }

        return null;
    }

    private static bool LooksLikeInstagramHtml(string text) =>
        text.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("window._sharedData", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("window.__additionalDataLoaded", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("graphql", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static string? FindExecutable(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        return pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(dir => Path.Combine(dir, name)).FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> ExtractJsonFragments(string script)
    {
        var fragments = new List<string>();

        var jsonStart = script.IndexOf('{');
        if (jsonStart >= 0)
        {
            var fragment = ExtractBalancedFragment(script, jsonStart, '{', '}');
            if (fragment is not null)
                fragments.Add(fragment);
        }

        jsonStart = script.IndexOf('[');
        if (jsonStart < 0) return fragments;
        {
	        var fragment = ExtractBalancedFragment(script, jsonStart, '[', ']');
	        if (fragment is not null)
	        {
		        fragments.Add(fragment);
	        }
        }

        return fragments;
    }

    private static string? ExtractBalancedFragment(string text, int startIndex, char open, char close)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = startIndex; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                switch (ch)
                {
	                case '\\':
		                escaped = true;
		                continue;
	                case '"':
		                inString = false;
		                break;
                }
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == open)
                depth++;
            else if (ch == close)
            {
                depth--;
                if (depth == 0)
                {
                    return text[startIndex..(i + 1)];
                }
            }
        }

        return null;
    }

    private static void TryScanJson(string json, string sourceLabel, List<TrackCandidate> results, HashSet<string> seen)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            Walk(document.RootElement, sourceLabel, results, seen);
        }
        catch
        {
            // Instagram page payloads change often. Best-effort extraction is better than failing the whole import.
        }
    }

    private static void Walk(JsonElement element, string sourceLabel, List<TrackCandidate> results, HashSet<string> seen)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject().ToList();
                string? title = FindFirst(properties, "title", "name", "song_name", "track_name");
                string? artist = FindFirst(properties, "artist_name", "display_artist_name", "artist");
                string? album = FindFirst(properties, "album_name", "album", "collection_name", "release_name");
                var isMusicLike = properties.Any(p => p.Name.Contains("music", StringComparison.OrdinalIgnoreCase) ||
                                                     p.Name.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
                                                     p.Name.Contains("song", StringComparison.OrdinalIgnoreCase) ||
                                                     p.Name.Contains("track", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(title) && (isMusicLike || !string.IsNullOrWhiteSpace(artist) || !string.IsNullOrWhiteSpace(album)))
                    AddCandidate(results, seen, title!, artist, album, sourceLabel);

                foreach (var property in properties)
                    Walk(property.Value, sourceLabel, results, seen);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    Walk(child, sourceLabel, results, seen);
                break;
        }
    }

    private static string? FindFirst(IEnumerable<JsonProperty> properties, params string[] names)
    {
	    return (from name in names select properties.FirstOrDefault(p => p.NameEquals(name)) into match where match.Value.ValueKind == JsonValueKind.String select match.Value.GetString()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static void AddCandidate(List<TrackCandidate> results, HashSet<string> seen, string title, string? artist, string? album, string source)
    {
        var candidate = new TrackCandidate(
            title.Trim(),
            string.IsNullOrWhiteSpace(artist) ? null : artist.Trim(),
            string.IsNullOrWhiteSpace(album) ? null : album.Trim(),
            source);
        if (seen.Add(candidate.NormalizedKey))
            results.Add(candidate);
    }
}