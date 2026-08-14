using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Highlightify.Core;

namespace Highlightify.Integrations;

public sealed class SpotifyClient : IDisposable
{
	private const string PlaylistModifyScope = "playlist-modify-private playlist-modify-public playlist-read-private";

	private readonly string _clientId;
	private readonly string _redirectUri;
	private readonly bool _openBrowser;
	private readonly HttpClient _httpClient;
	private SpotifyTokenCache? _token;

	public SpotifyClient(string clientId, string redirectUri, bool openBrowser)
	{
		_clientId = clientId;
		_redirectUri = redirectUri;
		_openBrowser = openBrowser;
		_httpClient = new HttpClient {BaseAddress = new Uri("https://api.spotify.com/v1/")};
	}

	public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
	{
		_token = await LoadCachedTokenAsync(cancellationToken);
		if (_token is not null && _token.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
		{
			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
			return;
		}

		if (_token?.RefreshToken is not null)
		{
			_token = await RefreshTokenAsync(_token.RefreshToken, cancellationToken);
			SaveCachedToken(_token);
			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
			return;
		}

		var pkce = Pkce.Create();
		var state = RandomNumberGenerator.GetHexString(16);
		var authUrl = BuildAuthorizeUrl(pkce.CodeChallenge, state);
		using var listener = new HttpListener();
		listener.Prefixes.Add(_redirectUri.EndsWith('/') ? _redirectUri : _redirectUri + "/");
		listener.Start();

		if (_openBrowser)
			TryOpenBrowser(authUrl);

		Console.WriteLine("Authorize Spotify access:");
		Console.WriteLine(authUrl);
		Console.WriteLine();
		Console.WriteLine("Waiting for the callback...");

		var contextTask = listener.GetContextAsync();
		var callback = await contextTask.WaitAsync(cancellationToken);
		var query = ParseQuery(callback.Request.Url?.Query ?? string.Empty);
		query.TryGetValue("code", out var code);
		query.TryGetValue("state", out var returnedState);

		await RespondAsync(callback);

		if (string.IsNullOrWhiteSpace(code) || returnedState != state)
			throw new InvalidOperationException("Spotify authorization failed or returned an invalid state.");

		_token = await ExchangeCodeAsync(code!, pkce.CodeVerifier, cancellationToken);
		SaveCachedToken(_token);
		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
	}

	public async Task<string> GetOrCreatePlaylistIdAsync(string playlistName, string? playlistId, CancellationToken cancellationToken = default)
	{
		if (!string.IsNullOrWhiteSpace(playlistId))
			return playlistId;

		using var response = await _httpClient.PostAsJsonAsync("me/playlists", new
		{
			name = playlistName,
			@public = false,
			description = "Created from Instagram story highlights by highlightify"
		}, cancellationToken);
		response.EnsureSuccessStatusCode();

		var created = await response.Content.ReadFromJsonAsync<SpotifyPlaylistResponse>(cancellationToken: cancellationToken)
		              ?? throw new InvalidOperationException("Spotify returned an empty playlist response.");
		return created.Id;
	}

	public async Task<string?> FindTrackUriAsync(TrackCandidate candidate, CancellationToken cancellationToken = default)
	{
		foreach (var query in BuildSearchQueries(candidate))
		{
			var encodedQuery = Uri.EscapeDataString(query);
			using var response = await _httpClient.GetAsync($"search?type=track&limit=10&q={encodedQuery}", cancellationToken);
			if (!response.IsSuccessStatusCode)
				continue;

			var payload = await response.Content.ReadFromJsonAsync<SpotifySearchResponse>(cancellationToken: cancellationToken);
			var track = ChooseBestTrack(candidate, payload?.Tracks?.Items);
			if (track?.Uri is not null)
				return track.Uri;
		}

		return null;
	}

	public async Task AddTracksToPlaylistAsync(string playlistId, IReadOnlyList<string> trackUris, CancellationToken cancellationToken = default)
	{
		foreach (var batch in trackUris.Chunk(100))
		{
			using var response = await _httpClient.PostAsJsonAsync($"playlists/{playlistId}/items", new {uris = batch}, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}

	private IEnumerable<string> BuildSearchQueries(TrackCandidate candidate)
	{
		if (!string.IsNullOrWhiteSpace(candidate.Artist) && !string.IsNullOrWhiteSpace(candidate.Album))
			yield return $"track:\"{candidate.Title}\" artist:\"{candidate.Artist}\" album:\"{candidate.Album}\"";

		if (!string.IsNullOrWhiteSpace(candidate.Artist))
		{
			yield return $"track:\"{candidate.Title}\" artist:\"{candidate.Artist}\"";
			yield return $"{candidate.Title} {candidate.Artist}";
		}

		if (!string.IsNullOrWhiteSpace(candidate.Album))
		{
			yield return $"track:\"{candidate.Title}\" album:\"{candidate.Album}\"";
			yield return $"{candidate.Title} {candidate.Album}";
		}

		yield return candidate.Title;
	}

	private static SpotifyTrack? ChooseBestTrack(TrackCandidate candidate, IReadOnlyList<SpotifyTrack>? tracks)
	{
		if (tracks is null || tracks.Count == 0)
			return null;

		var best = tracks
			.Select(track => new
			{
				Track = track,
				Score = ScoreTrack(candidate, track)
			})
			.OrderByDescending(x => x.Score)
			.FirstOrDefault();

		return best?.Track;
	}

	private static int ScoreTrack(TrackCandidate candidate, SpotifyTrack track)
	{
		var score = 0;
		var candidateTitle = Normalize(candidate.Title);
		var candidateArtist = Normalize(candidate.Artist);
		var candidateAlbum = Normalize(candidate.Album);
		var trackName = Normalize(track.Name);
		var albumName = Normalize(track.Album?.Name);

		if (trackName == candidateTitle)
			score += 100;
		else if (trackName.Contains(candidateTitle, StringComparison.OrdinalIgnoreCase) ||
		         candidateTitle.Contains(trackName, StringComparison.OrdinalIgnoreCase))
			score += 60;

		if (!string.IsNullOrWhiteSpace(candidateArtist))
		{
			var artistMatch = track.Artists?.Any(a =>
			{
				var artistName = Normalize(a.Name);
				return artistName == candidateArtist ||
				       artistName.Contains(candidateArtist, StringComparison.OrdinalIgnoreCase) ||
				       candidateArtist.Contains(artistName, StringComparison.OrdinalIgnoreCase);
			}) == true;

			if (artistMatch)
				score += 80;
		}

		if (!string.IsNullOrWhiteSpace(candidateAlbum))
		{
			if (albumName == candidateAlbum)
				score += 25;
			else if (albumName.Contains(candidateAlbum, StringComparison.OrdinalIgnoreCase) ||
			         candidateAlbum.Contains(albumName, StringComparison.OrdinalIgnoreCase))
				score += 10;
		}

		if (track.ExternalIds?.Isrc is not null)
			score += 1;

		return score;
	}

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();

	private string BuildAuthorizeUrl(string codeChallenge, string state)
	{
		var query = new Dictionary<string, string?>
		{
			["client_id"] = _clientId,
			["response_type"] = "code",
			["redirect_uri"] = _redirectUri,
			["code_challenge_method"] = "S256",
			["code_challenge"] = codeChallenge,
			["state"] = state,
			["scope"] = PlaylistModifyScope
		};

		var queryString = string.Join("&", query
			.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
			.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

		return $"https://accounts.spotify.com/authorize?{queryString}";
	}

	private static async Task RespondAsync(HttpListenerContext context)
	{
		const string message = """
		                       <html>
		                         <body>
		                           <h1>Spotify authorization complete</h1>
		                           <p>You can close this window and return to highlightify.</p>
		                         </body>
		                       </html>
		                       """;

		var bytes = Encoding.UTF8.GetBytes(message);
		context.Response.StatusCode = 200;
		context.Response.ContentType = "text/html; charset=utf-8";
		context.Response.ContentLength64 = bytes.Length;
		await context.Response.OutputStream.WriteAsync(bytes);
		context.Response.Close();
	}

	private async Task<SpotifyTokenCache> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string?>
		{
			["client_id"] = _clientId,
			["grant_type"] = "authorization_code",
			["code"] = code,
			["redirect_uri"] = _redirectUri,
			["code_verifier"] = codeVerifier
		};

		using var response = await _httpClient.PostAsync(
			"https://accounts.spotify.com/api/token",
			new FormUrlEncodedContent(form.Where(kvp => kvp.Value is not null).Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value!))),
			cancellationToken);
		response.EnsureSuccessStatusCode();

		var token = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(cancellationToken: cancellationToken)
		            ?? throw new InvalidOperationException("Spotify returned an empty token response.");

		return SpotifyTokenCache.From(token);
	}

	private async Task<SpotifyTokenCache> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string?>
		{
			["client_id"] = _clientId,
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken
		};

		using var response = await _httpClient.PostAsync(
			"https://accounts.spotify.com/api/token",
			new FormUrlEncodedContent(form.Where(kvp => kvp.Value is not null).Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value!))),
			cancellationToken);
		response.EnsureSuccessStatusCode();

		var token = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(cancellationToken: cancellationToken)
		            ?? throw new InvalidOperationException("Spotify returned an empty refresh response.");

		return SpotifyTokenCache.From(token, refreshToken);
	}

	private async Task<SpotifyTokenCache?> LoadCachedTokenAsync(CancellationToken cancellationToken)
	{
		var path = GetCachePath();
		if (!File.Exists(path))
			return null;

		var json = await File.ReadAllTextAsync(path, cancellationToken);
		return JsonSerializer.Deserialize<SpotifyTokenCache>(json, SpotifyJson.Options);
	}

	private void SaveCachedToken(SpotifyTokenCache token)
	{
		var path = GetCachePath();
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, JsonSerializer.Serialize(token, SpotifyJson.Options));
	}

	private static string GetCachePath()
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		return Path.Combine(home, ".config", "highlightify", "spotify-token.json");
	}

	private static void TryOpenBrowser(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url) {UseShellExecute = true});
		}
		catch
		{
			// If the environment cannot launch a browser, the URL is still printed for manual copy/paste.
		}
	}

	public void Dispose()
	{
		_httpClient.Dispose();
	}

	private sealed record SpotifyPlaylistResponse(string Id);

	private sealed record SpotifySearchResponse(SpotifyTrackPage? Tracks);

	private sealed record SpotifyTrackPage(IReadOnlyList<SpotifyTrack>? Items);

	private sealed record SpotifyTrack(
		string Name,
		IReadOnlyList<SpotifyArtist>? Artists,
		SpotifyAlbum? Album,
		SpotifyExternalIds? ExternalIds,
		string Uri);

	private sealed record SpotifyArtist(string Name);

	private sealed record SpotifyAlbum(string Name);

	private sealed record SpotifyExternalIds([property: JsonPropertyName("isrc")] string? Isrc);

	private sealed record SpotifyTokenResponse(
		[property: JsonPropertyName("access_token")]
		string AccessToken,
		[property: JsonPropertyName("token_type")]
		string TokenType,
		[property: JsonPropertyName("expires_in")]
		int ExpiresIn,
		[property: JsonPropertyName("refresh_token")]
		string? RefreshToken,
		[property: JsonPropertyName("scope")] string? Scope);

	private sealed record SpotifyTokenCache(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAtUtc)
	{
		public static SpotifyTokenCache From(SpotifyTokenResponse response, string? fallbackRefreshToken = null)
		{
			return new SpotifyTokenCache(
				response.AccessToken,
				response.RefreshToken ?? fallbackRefreshToken,
				DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn));
		}
	}

	private static class SpotifyJson
	{
		public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
		{
			WriteIndented = true
		};
	}

	private static Dictionary<string, string> ParseQuery(string queryString)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var trimmed = queryString.TrimStart('?');
		if (string.IsNullOrWhiteSpace(trimmed))
			return result;

		foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var equalsIndex = pair.IndexOf('=');
			if (equalsIndex < 0)
				continue;

			var key = Uri.UnescapeDataString(pair[..equalsIndex].Replace('+', ' '));
			var value = Uri.UnescapeDataString(pair[(equalsIndex + 1)..].Replace('+', ' '));
			result[key] = value;
		}

		return result;
	}

	private sealed record Pkce(string CodeVerifier, string CodeChallenge)
	{
		public static Pkce Create()
		{
			var bytes = RandomNumberGenerator.GetBytes(64);
			var verifier = Base64UrlEncode(bytes);
			var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
			var challenge = Base64UrlEncode(challengeBytes);
			return new Pkce(verifier, challenge);
		}

		private static string Base64UrlEncode(byte[] bytes) =>
			Convert.ToBase64String(bytes)
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');
	}
}