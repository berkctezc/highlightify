namespace Highlightify.Application;

public static class HighlightifyApp
{
	public static async Task<int> RunAsync(string[] args)
	{
		if (args.Any(a => a is "--help" or "-h"))
		{
			PrintUsage();
			return 0;
		}

		var options = AppOptions.Parse(args);
		if (!options.IsValid(out var validationError))
		{
			await Console.Error.WriteLineAsync(validationError);
			await Console.Error.WriteLineAsync();
			PrintUsage();
			return 1;
		}

		var instagram = new InstagramHighlightFetcher();
		var allCandidates = new List<TrackCandidate>();
		var browserSpec = options.InstagramCookiesFromBrowser
		                  ?? BrowserCookieLoader.InferFirefoxBrowserSpec(options.InstagramCookiePath);

		foreach (var source in options.HighlightSources)
		{
			IReadOnlyList<TrackCandidate> candidates;
			try
			{
				if (File.Exists(source))
				{
					var html = await File.ReadAllTextAsync(source);
					candidates = instagram.ExtractCandidates(html, source);
				}
				else if (!string.IsNullOrWhiteSpace(browserSpec))
				{
					var highlightUrl = HighlightSourceResolver.ResolveInstagramHighlightUrl(source);
					candidates = await instagram.FetchCandidatesViaYtDlpAsync(highlightUrl, browserSpec);
				}
				else
				{
					var highlightUrl = HighlightSourceResolver.ResolveInstagramHighlightUrl(source);
					candidates = await instagram.FetchCandidatesAsync(highlightUrl, options.InstagramCookieHeader);
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
			{
				Console.Error.WriteLine($"Instagram fetch failed for '{source}': {ex.Message}");
				return 4;
			}

			allCandidates.AddRange(candidates);
		}

		if (allCandidates.Count == 0)
		{
			await Console.Error.WriteLineAsync(options.InstagramCookieHeader is null
				? "No songs were found. If the account is private, pass --instagram-cookie, --instagram-cookie-file, or --instagram-cookie-path so Instagram can return the highlight data."
				: "No songs were found in the supplied highlights.");
			return 2;
		}

		var distinctCandidates = allCandidates
			.DistinctBy(c => c.NormalizedKey)
			.ToList();

		Console.WriteLine($"Found {distinctCandidates.Count} candidate songs.");

		if (options.DryRun)
		{
			foreach (var candidate in distinctCandidates)
				Console.WriteLine($"- {candidate.DisplayTitle}");

			return 0;
		}

		using var spotify = new SpotifyClient(
			options.SpotifyClientId!,
			options.RedirectUri,
			options.OpenBrowser);

		await spotify.AuthenticateAsync();

		var playlistId = await spotify.GetOrCreatePlaylistIdAsync(options.PlaylistName, options.PlaylistId);
		var uris = new List<string>();
		var unresolved = new List<TrackCandidate>();

		foreach (var candidate in distinctCandidates)
		{
			var uri = await spotify.FindTrackUriAsync(candidate);
			if (uri is null)
			{
				unresolved.Add(candidate);
				continue;
			}

			uris.Add(uri);
			Console.WriteLine($"Matched: {candidate.DisplayTitle}");
		}

		if (uris.Count == 0)
		{
			Console.Error.WriteLine("No Spotify matches were found.");
			return 3;
		}

		await spotify.AddTracksToPlaylistAsync(playlistId, uris);

		Console.WriteLine();
		Console.WriteLine($"Added {uris.Count} track(s) to playlist {playlistId}.");

		if (unresolved.Count <= 0)
			return 0;
		Console.WriteLine();
		Console.WriteLine("Unmatched candidates:");
		foreach (var candidate in unresolved)
			Console.WriteLine($"- {candidate.DisplayTitle}");

		return 0;
	}

	private static void PrintUsage()
	{
		Console.WriteLine("""
		                  highlightify fetches songs mentioned in Instagram story highlights and adds them to a Spotify playlist.

		                  Usage:
		                    highlightify --highlight <id> [--highlight <id> ...] --spotify-client-id <id> [options]
		                    highlightify --html <file> [--html <file> ...] --spotify-client-id <id> [options]

		                  Required:
		                    --spotify-client-id <id>     Spotify app client id

		                  Highlight input:
		                    --highlight <id>             Instagram highlight ID to fetch
		                    --html <file>                Local HTML export to parse instead of fetching a URL
		                    --instagram-cookie <value>    Raw Cookie header for Instagram requests
		                    --instagram-cookie-file <f>   File containing raw Cookie header or Netscape cookie lines
		                    --instagram-cookie-path <f>   Firefox profile directory or cookies.sqlite file
		                    --instagram-cookies-from-browser <spec>  Browser source like firefox or firefox:/path/to/profile (preferred)

		                  Spotify output:
		                    --playlist-name <name>       Playlist name to create or reuse; defaults to "Instagram Highlights"
		                    --playlist-id <id>           Existing playlist id to append to
		                    --redirect-uri <uri>         OAuth redirect URI; defaults to http://127.0.0.1:54321/callback/
		                    --no-browser                 Print auth URL instead of opening a browser

		                  Other:
		                    --dry-run                    Print matches without modifying Spotify
		                    --help, -h                   Show this help
		                  """);
	}
}