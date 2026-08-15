namespace Highlightify.Web.Services;

public sealed record SpotifySettings(
	string? ClientId,
	string RedirectUri,
	string FrontendUrl)
{
	public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}

public sealed class SpotifyWebService(
	IHttpClientFactory httpClientFactory,
	SpotifySessionStore sessionStore,
	ArtworkSimilarityService artworkSimilarity,
	SpotifySettings settings)
{
	private const string Scope = "playlist-modify-private playlist-modify-public playlist-read-private user-read-private";

	public bool IsConfigured => settings.IsConfigured;

	public string CreateAuthorizationUrl(string sessionId, string returnPath)
	{
		EnsureConfigured();

		var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
		var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
		var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
		var safeReturnPath = NormalizeReturnPath(returnPath);

		sessionStore.SetPending(state, new PendingSpotifyAuthorization(
			sessionId,
			verifier,
			settings.RedirectUri,
			safeReturnPath,
			DateTimeOffset.UtcNow.AddMinutes(10)));

		return QueryHelpers.AddQueryString("https://accounts.spotify.com/authorize", new Dictionary<string, string?>
		{
			["client_id"] = settings.ClientId,
			["response_type"] = "code",
			["redirect_uri"] = settings.RedirectUri,
			["scope"] = Scope,
			["state"] = state,
			["code_challenge_method"] = "S256",
			["code_challenge"] = challenge,
			["show_dialog"] = "false"
		});
	}

	public async Task<string> CompleteAuthorizationAsync(
		string sessionId,
		string code,
		string state,
		CancellationToken cancellationToken)
	{
		EnsureConfigured();
		var pending = sessionStore.TakePending(state)
		              ?? throw new InvalidOperationException("Spotify bağlantı isteğinin süresi doldu. Lütfen yeniden deneyin.");

		if (!CryptographicOperations.FixedTimeEquals(
		    Encoding.UTF8.GetBytes(pending.SessionId),
		    Encoding.UTF8.GetBytes(sessionId)))
			throw new InvalidOperationException("Spotify bağlantı oturumu doğrulanamadı.");

		using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["client_id"] = settings.ClientId!,
				["grant_type"] = "authorization_code",
				["code"] = code,
				["redirect_uri"] = pending.RedirectUri,
				["code_verifier"] = pending.CodeVerifier
			})
		};

		using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
		await EnsureSuccessAsync(response, "Spotify yetkilendirmesi tamamlanamadı", cancellationToken);
		var token = await ReadTokenAsync(response, cancellationToken);
		sessionStore.SetToken(sessionId, token);
		return pending.ReturnPath;
	}

	public async Task<SpotifyConnectionResponse> GetConnectionAsync(string sessionId, CancellationToken cancellationToken)
	{
		if (!IsConfigured)
			return new SpotifyConnectionResponse(false, false, null);

		if (sessionStore.GetToken(sessionId) is null)
			return new SpotifyConnectionResponse(false, true, null);

		try
		{
			using var response = await SendAsync(sessionId, HttpMethod.Get, "me", null, cancellationToken);
			using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
			var root = payload.RootElement;
			var id = ReadString(root, "id") ?? "spotify-user";
			var displayName = ReadString(root, "display_name") ?? id;
			var imageUrl = ReadFirstImage(root);
			var externalUrl = ReadNestedString(root, "external_urls", "spotify");
			return new SpotifyConnectionResponse(true, true, new SpotifyProfileResponse(id, displayName, imageUrl, externalUrl));
		}
		catch (UnauthorizedAccessException)
		{
			return new SpotifyConnectionResponse(false, true, null);
		}
	}

	public void Disconnect(string sessionId) => sessionStore.RemoveToken(sessionId);

	public async Task<IReadOnlyList<SpotifyPlaylistResponse>> GetPlaylistsAsync(
		string sessionId,
		CancellationToken cancellationToken)
	{
		using var response = await SendAsync(sessionId, HttpMethod.Get, "me/playlists?limit=50", null, cancellationToken);
		using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
		if (!payload.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
			return [];

		var playlists = new List<SpotifyPlaylistResponse>();
		foreach (var item in items.EnumerateArray())
		{
			var id = ReadString(item, "id");
			var name = ReadString(item, "name");
			if (id is null || name is null)
				continue;

			playlists.Add(new SpotifyPlaylistResponse(
				id,
				name,
				ReadFirstImage(item),
				ReadNestedInt(item, "tracks", "total"),
				ReadBool(item, "public"),
				ReadNestedString(item, "external_urls", "spotify")));
		}

		return playlists;
	}

	public async Task<IReadOnlyList<SpotifyTrackResponse>> FindTrackMatchesAsync(
		string sessionId,
		TrackCandidate candidate,
		CancellationToken cancellationToken)
	{
		var matches = new Dictionary<string, SpotifyTrackResponse>(StringComparer.Ordinal);
		foreach (var query in BuildSearchQueries(candidate))
		{
			using var response = await SendAsync(
				sessionId,
				HttpMethod.Get,
				$"search?type=track&limit=10&q={Uri.EscapeDataString(query)}",
				null,
				cancellationToken);

			using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
			if (!payload.RootElement.TryGetProperty("tracks", out var tracks) ||
			    !tracks.TryGetProperty("items", out var items) ||
			    items.ValueKind != JsonValueKind.Array)
				continue;

			foreach (var item in items.EnumerateArray())
			{
				var parsed = ParseTrack(item, candidate);
				if (parsed is null)
					continue;

				if (!matches.TryGetValue(parsed.Id, out var current) || parsed.MatchScore > current.MatchScore)
					matches[parsed.Id] = parsed;
			}

			if (matches.Values.Any(match => match.MatchScore >= 180))
				break;
		}

		var rankedMatches = await artworkSimilarity.ApplyAsync(candidate, matches.Values.ToList(), cancellationToken);
		return rankedMatches
			.OrderByDescending(match => match.MatchScore)
			.ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
			.Take(5)
			.ToList();
	}

	public async Task<(string Id, string? Url)> CreatePlaylistAsync(
		string sessionId,
		string name,
		bool isPublic,
		CancellationToken cancellationToken)
	{
		var trimmedName = name.Trim();
		if (trimmedName.Length is < 1 or > 100)
			throw new ArgumentException("Playlist adı 1 ile 100 karakter arasında olmalı.");

		using var response = await SendAsync(sessionId, HttpMethod.Post, "me/playlists", new
		{
			name = trimmedName,
			@public = isPublic,
			description = "Instagram Highlight müziklerinden Highlightify ile oluşturuldu."
		}, cancellationToken);

		using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
		var id = ReadString(payload.RootElement, "id")
		         ?? throw new InvalidOperationException("Spotify playlist kimliği döndürmedi.");
		return (id, ReadNestedString(payload.RootElement, "external_urls", "spotify"));
	}

	public async Task AddTracksAsync(
		string sessionId,
		string playlistId,
		IReadOnlyList<string> trackUris,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(playlistId))
			throw new ArgumentException("Playlist seçilmedi.");

		foreach (var batch in trackUris.Distinct(StringComparer.Ordinal).Chunk(100))
		{
			using var response = await SendAsync(
				sessionId,
				HttpMethod.Post,
				$"playlists/{Uri.EscapeDataString(playlistId)}/items",
				new { uris = batch },
				cancellationToken);
		}
	}

	private async Task<HttpResponseMessage> SendAsync(
		string sessionId,
		HttpMethod method,
		string path,
		object? body,
		CancellationToken cancellationToken)
	{
		var accessToken = await GetAccessTokenAsync(sessionId, cancellationToken);
		var request = new HttpRequestMessage(method, path);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		if (body is not null)
			request.Content = JsonContent.Create(body);

		var response = await httpClientFactory.CreateClient("spotify").SendAsync(request, cancellationToken);
		if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
		{
			response.Dispose();
			sessionStore.RemoveToken(sessionId);
			throw new UnauthorizedAccessException("Spotify oturumunun süresi doldu. Lütfen yeniden bağlanın.");
		}

		try
		{
			await EnsureSuccessAsync(response, "Spotify isteği başarısız oldu", cancellationToken);
			return response;
		}
		catch
		{
			response.Dispose();
			throw;
		}
	}

	private async Task<string> GetAccessTokenAsync(string sessionId, CancellationToken cancellationToken)
	{
		var token = sessionStore.GetToken(sessionId)
		            ?? throw new UnauthorizedAccessException("Önce Spotify hesabınızı bağlayın.");
		if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
			return token.AccessToken;

		var refreshLock = sessionStore.GetRefreshLock(sessionId);
		await refreshLock.WaitAsync(cancellationToken);
		try
		{
			token = sessionStore.GetToken(sessionId)
			        ?? throw new UnauthorizedAccessException("Spotify oturumunun süresi doldu.");
			if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
				return token.AccessToken;
			if (string.IsNullOrWhiteSpace(token.RefreshToken))
			{
				sessionStore.RemoveToken(sessionId);
				throw new UnauthorizedAccessException("Spotify oturumunu yenilemek için yeniden bağlanın.");
			}

			using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
			{
				Content = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					["client_id"] = settings.ClientId!,
					["grant_type"] = "refresh_token",
					["refresh_token"] = token.RefreshToken
				})
			};

			using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
			await EnsureSuccessAsync(response, "Spotify oturumu yenilenemedi", cancellationToken);
			var refreshed = await ReadTokenAsync(response, cancellationToken, token.RefreshToken);
			sessionStore.SetToken(sessionId, refreshed);
			return refreshed.AccessToken;
		}
		finally
		{
			refreshLock.Release();
		}
	}

	private static async Task<SpotifyTokenSession> ReadTokenAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken,
		string? fallbackRefreshToken = null)
	{
		using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
		var accessToken = ReadString(payload.RootElement, "access_token")
		                  ?? throw new InvalidOperationException("Spotify erişim anahtarı döndürmedi.");
		var refreshToken = ReadString(payload.RootElement, "refresh_token") ?? fallbackRefreshToken;
		var expiresIn = ReadInt(payload.RootElement, "expires_in", 3600);
		return new SpotifyTokenSession(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
	}

	private static async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		string message,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
			return;

		var details = await response.Content.ReadAsStringAsync(cancellationToken);
		if (details.Length > 500)
			details = details[..500];
		throw new InvalidOperationException($"{message} ({(int)response.StatusCode}). {details}".Trim());
	}

	private static SpotifyTrackResponse? ParseTrack(JsonElement item, TrackCandidate candidate)
	{
		var id = ReadString(item, "id");
		var uri = ReadString(item, "uri");
		var name = ReadString(item, "name");
		if (id is null || uri is null || name is null)
			return null;

		var artists = new List<string>();
		if (item.TryGetProperty("artists", out var artistItems) && artistItems.ValueKind == JsonValueKind.Array)
		{
			artists.AddRange(artistItems.EnumerateArray()
				.Select(artist => ReadString(artist, "name"))
				.Where(artist => !string.IsNullOrWhiteSpace(artist))!);
		}

		var album = item.TryGetProperty("album", out var albumItem) ? ReadString(albumItem, "name") ?? "" : "";
		var imageUrl = item.TryGetProperty("album", out albumItem) ? ReadFirstImage(albumItem) : null;
		var artist = string.Join(", ", artists);
		var durationMs = ReadInt(item, "duration_ms", 0);
		var popularity = ReadInt(item, "popularity", 0);
		var score = Score(candidate, name, artists, album, durationMs, popularity);

		return new SpotifyTrackResponse(
			id,
			uri,
			name,
			artist,
			album,
			imageUrl,
			ReadNestedString(item, "external_urls", "spotify"),
			durationMs,
			ReadBool(item, "explicit"),
			score);
	}

	private static int Score(
		TrackCandidate candidate,
		string trackName,
		IReadOnlyList<string> artists,
		string album,
		int durationMs,
		int popularity)
	{
		var score = 0;
		var candidateTitle = Normalize(candidate.Title);
		var candidateArtist = Normalize(candidate.Artist);
		var candidateAlbum = Normalize(candidate.Album);
		var normalizedTrack = Normalize(trackName);
		var normalizedAlbum = Normalize(album);

		if (normalizedTrack == candidateTitle)
			score += 100;
		else if (ContainsEither(normalizedTrack, candidateTitle))
			score += 60;

		if (!string.IsNullOrWhiteSpace(candidateArtist) && artists.Any(artist =>
		    Normalize(artist) == candidateArtist || ContainsEither(Normalize(artist), candidateArtist)))
			score += 80;

		if (!string.IsNullOrWhiteSpace(candidateAlbum))
		{
			if (normalizedAlbum == candidateAlbum)
				score += 25;
			else if (ContainsEither(normalizedAlbum, candidateAlbum))
				score += 10;
		}

		if (candidate.DurationMs is > 0 && durationMs > 0)
		{
			var difference = Math.Abs(candidate.DurationMs.Value - durationMs);
			score += Math.Max(0, 50 - difference / 100);
		}
		score += Math.Clamp(popularity, 0, 100) / 10;

		return score;
	}

	private static IEnumerable<string> BuildSearchQueries(TrackCandidate candidate)
	{
		if (!string.IsNullOrWhiteSpace(candidate.Artist) && !string.IsNullOrWhiteSpace(candidate.Album))
			yield return $"track:\"{candidate.Title}\" artist:\"{candidate.Artist}\" album:\"{candidate.Album}\"";
		if (!string.IsNullOrWhiteSpace(candidate.Artist))
		{
			yield return $"track:\"{candidate.Title}\" artist:\"{candidate.Artist}\"";
			yield return $"{candidate.Title} {candidate.Artist}";
		}
		if (!string.IsNullOrWhiteSpace(candidate.Album))
			yield return $"{candidate.Title} {candidate.Album}";
		yield return candidate.Title;
	}

	private void EnsureConfigured()
	{
		if (!IsConfigured)
			throw new InvalidOperationException("SPOTIFY_CLIENT_ID yapılandırılmadı.");
	}

	private static string NormalizeReturnPath(string returnPath) =>
		string.IsNullOrWhiteSpace(returnPath) || !returnPath.StartsWith('/') || returnPath.StartsWith("//")
			? "/"
			: returnPath;

	private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

	private static bool ContainsEither(string left, string right) =>
		!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
		(left.Contains(right, StringComparison.OrdinalIgnoreCase) || right.Contains(left, StringComparison.OrdinalIgnoreCase));

	private static string Base64UrlEncode(byte[] bytes) =>
		Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	private static string? ReadString(JsonElement element, string property) =>
		element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static int ReadInt(JsonElement element, string property, int fallback) =>
		element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

	private static bool ReadBool(JsonElement element, string property) =>
		element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

	private static string? ReadNestedString(JsonElement element, string parent, string property) =>
		element.TryGetProperty(parent, out var nested) ? ReadString(nested, property) : null;

	private static int ReadNestedInt(JsonElement element, string parent, string property) =>
		element.TryGetProperty(parent, out var nested) ? ReadInt(nested, property, 0) : 0;

	private static string? ReadFirstImage(JsonElement element)
	{
		if (!element.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
			return null;

		foreach (var image in images.EnumerateArray())
		{
			var url = ReadString(image, "url");
			if (url is not null)
				return url;
		}

		return null;
	}
}
