namespace Highlightify.Integrations;

public sealed class InstagramHighlightFetcher
{
	private static readonly Regex ScriptBlockRegex = new(@"<script\b[^>]*>(?<script>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex PairFallbackRegex = new(
		"\"(?:artist_name|display_artist_name|artist)\"\\s*:\\s*\"(?<artist>[^\"]{1,200})\".{0,500}?\"(?:title|name|song_name|track_name)\"\\s*:\\s*\"(?<title>[^\"]{1,200})\"",
		RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly HttpClient _httpClient;

	public InstagramHighlightFetcher(HttpClient? httpClient = null)
	{
		_httpClient = httpClient ?? new HttpClient();
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
		if ((int) response.StatusCode >= 300 && (int) response.StatusCode < 400)
		{
			var location = response.Headers.Location?.ToString();
			throw new InvalidOperationException(
				$"Instagram redirected the highlight request with {(int) response.StatusCode} {response.ReasonPhrase}."
				+ (string.IsNullOrWhiteSpace(location) ? string.Empty : $" Location: {location}.")
				+ " This usually means the session cookies are expired, incomplete, or the account requires a fresh login for this profile.");
		}

		if (!response.IsSuccessStatusCode)
			throw new InvalidOperationException(
				$"Instagram returned {(int) response.StatusCode} {response.ReasonPhrase} for the highlight request.");

		var html = await response.Content.ReadAsStringAsync(cancellationToken);
		return ExtractCandidates(html, highlightUrl);
	}

	public async Task<IReadOnlyList<TrackCandidate>> FetchCandidatesViaYtDlpAsync(
		string highlightUrl,
		string cookiesFromBrowser,
		CancellationToken cancellationToken = default)
	{
		var payloads = await FetchPayloadsViaYtDlpAsync(highlightUrl, cookiesFromBrowser, cancellationToken);
		return ExtractCandidates(payloads, highlightUrl);
	}

	public IReadOnlyList<TrackCandidate> ExtractCandidates(IEnumerable<string> payloads, string sourceLabel)
	{
		return
		[
			.. payloads
				.SelectMany(payload => ExtractCandidates(payload, sourceLabel))
				.GroupBy(candidate => candidate.NormalizedKey, StringComparer.OrdinalIgnoreCase)
				.Select(group => group
					.OrderByDescending(candidate => candidate.DurationMs.HasValue)
					.ThenByDescending(candidate => !string.IsNullOrWhiteSpace(candidate.ArtworkUrl))
					.ThenByDescending(candidate => !string.IsNullOrWhiteSpace(candidate.Album))
					.First())
		];
	}

	public IReadOnlyList<TrackCandidate> ExtractCandidates(string html, string sourceLabel)
	{
		var results = new List<TrackCandidate>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var trimmed = html.AsSpan().TrimStart();
		if (!trimmed.IsEmpty && trimmed[0] is '{' or '[')
			TryScanJson(html, sourceLabel, results, seen);

		foreach (Match match in ScriptBlockRegex.Matches(html))
		{
			var script = WebUtility.HtmlDecode(match.Groups["script"].Value);
			foreach (var fragment in ExtractJsonFragments(script))
				TryScanJson(fragment, sourceLabel, results, seen);
		}

		// The fallback regex can accidentally pair fields from adjacent JSON objects.
		// Only use it when the structured script scan found no candidates at all.
		if (results.Count != 0) return results;
		foreach (Match match in PairFallbackRegex.Matches(html))
			AddCandidate(results, seen, match.Groups["title"].Value, match.Groups["artist"].Value, null, sourceLabel, null, null);

		return results;
	}

	private static async Task<IReadOnlyList<string>> FetchPayloadsViaYtDlpAsync(
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
			await stdoutTask;
			var stderr = await stderrTask;

			if (process.ExitCode != 0)
			{
				if (RequiresFreshInstagramSession(stderr))
					throw new InvalidOperationException(
						"Instagram session could not be validated. Select the browser where you viewed the story, "
						+ "make sure you are signed in to Instagram in that browser, and try again.");

				throw new InvalidOperationException(
					"Instagram content could not be fetched using the browser session. The story may have been deleted or expired.");
			}

			var payloads = ReadWrittenPayloads(tempDir);
			return payloads.Count > 0
				? payloads
				: throw new InvalidOperationException("yt-dlp finished, but it did not produce a readable Instagram response.");
		}
		finally
		{
			TryDeleteDirectory(tempDir);
		}
	}

	private static bool RequiresFreshInstagramSession(string error)
	{
		return error.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
		       error.Contains("cookies-from-browser", StringComparison.OrdinalIgnoreCase) ||
		       error.Contains("cookies for", StringComparison.OrdinalIgnoreCase) ||
		       error.Contains("cookie database", StringComparison.OrdinalIgnoreCase) ||
		       error.Contains("failed to decrypt", StringComparison.OrdinalIgnoreCase);
	}

	private static IReadOnlyList<string> ReadWrittenPayloads(string root)
	{
		var candidates = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
			.Select(path => new FileInfo(path))
			.Where(file => file.Length is > 0 and < 10 * 1024 * 1024)
			.OrderByDescending(file => file.Length)
			.ThenByDescending(file => file.LastWriteTimeUtc)
			.ToList();

		var payloads = new List<string>();
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

			if (LooksLikeInstagramPayload(text))
				payloads.Add(text);
		}

		return payloads;
	}

	private static bool LooksLikeInstagramPayload(string text)
	{
		var trimmed = text.AsSpan().TrimStart();
		return (!trimmed.IsEmpty && trimmed[0] is '{' or '[') ||
		       text.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
		       text.Contains("window._sharedData", StringComparison.OrdinalIgnoreCase) ||
		       text.Contains("window.__additionalDataLoaded", StringComparison.OrdinalIgnoreCase) ||
		       text.Contains("graphql", StringComparison.OrdinalIgnoreCase);
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, true);
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

		var extensions = OperatingSystem.IsWindows()
			? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [".exe", ".cmd", ".bat"])
			: [string.Empty];

		foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			foreach (var extension in extensions)
			{
				var candidate = Path.Combine(directory, name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : $"{name}{extension}");
				if (File.Exists(candidate))
					return candidate;
			}
		}

		return null;
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
			if (fragment is not null) fragments.Add(fragment);
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
			{
				depth++;
			}
			else if (ch == close)
			{
				depth--;
				if (depth == 0) return text[startIndex..(i + 1)];
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
				var title = FindFirst(properties, "title", "name", "song_name", "track_name");
				var artist = FindFirst(properties, "artist_name", "display_artist", "display_artist_name", "artist");
				var album = FindFirst(properties, "album_name", "album", "collection_name", "release_name");
				var durationMs = FindFirstInt(properties, "duration_in_ms", "duration_ms");
				var artworkUrl = FindFirst(properties, "cover_artwork_uri", "cover_artwork_thumbnail_uri");
				var isMusicLike = properties.Any(p => p.Name.Contains("music", StringComparison.OrdinalIgnoreCase) ||
				                                      p.Name.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
				                                      p.Name.Contains("song", StringComparison.OrdinalIgnoreCase) ||
				                                      p.Name.Contains("track", StringComparison.OrdinalIgnoreCase));

				if (!string.IsNullOrWhiteSpace(title) && (isMusicLike || !string.IsNullOrWhiteSpace(artist) || !string.IsNullOrWhiteSpace(album)))
					AddCandidate(results, seen, title, artist, album, sourceLabel, durationMs, artworkUrl);

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

	private static int? FindFirstInt(IEnumerable<JsonProperty> properties, params string[] names)
	{
		foreach (var name in names)
		{
			var match = properties.FirstOrDefault(property => property.NameEquals(name));
			if (match.Value.ValueKind == JsonValueKind.Number && match.Value.TryGetInt32(out var value) && value > 0)
				return value;
		}

		return null;
	}

	private static void AddCandidate(
		List<TrackCandidate> results,
		HashSet<string> seen,
		string title,
		string? artist,
		string? album,
		string source,
		int? durationMs,
		string? artworkUrl)
	{
		var candidate = new TrackCandidate(
			title.Trim(),
			string.IsNullOrWhiteSpace(artist) ? null : artist.Trim(),
			string.IsNullOrWhiteSpace(album) ? null : album.Trim(),
			source,
			durationMs,
			string.IsNullOrWhiteSpace(artworkUrl) ? null : artworkUrl.Trim());
		if (seen.Add(candidate.NormalizedKey))
			results.Add(candidate);
	}
}