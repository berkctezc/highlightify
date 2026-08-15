namespace Highlightify.Web.Services;

public sealed record SpotifyTokenSession(
	string AccessToken,
	string? RefreshToken,
	DateTimeOffset ExpiresAt);

public sealed record PendingSpotifyAuthorization(
	string SessionId,
	string CodeVerifier,
	string RedirectUri,
	string ReturnPath,
	DateTimeOffset ExpiresAt);

public sealed class SpotifySessionStore
{
	private readonly ConcurrentDictionary<string, SpotifyTokenSession> _tokens = new();
	private readonly ConcurrentDictionary<string, PendingSpotifyAuthorization> _pending = new();
	private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();
	private readonly IDataProtector _tokenProtector;
	private readonly IDataProtector _pendingProtector;
	private readonly ILogger<SpotifySessionStore> _logger;
	private readonly string _tokenStoragePath;
	private readonly string _pendingStoragePath;
	private readonly object _persistenceLock = new();

	public SpotifySessionStore(
		IDataProtectionProvider dataProtectionProvider,
		IHostEnvironment environment,
		ILogger<SpotifySessionStore> logger)
	{
		_tokenProtector = dataProtectionProvider.CreateProtector("spotify-session-token-v1");
		_pendingProtector = dataProtectionProvider.CreateProtector("spotify-pending-authorization-v1");
		_logger = logger;
		_tokenStoragePath = Path.Combine(environment.ContentRootPath, "App_Data", "spotify-sessions.json");
		_pendingStoragePath = Path.Combine(environment.ContentRootPath, "App_Data", "spotify-pending.json");
		LoadTokens();
		LoadPending();
	}

	public SpotifyTokenSession? GetToken(string sessionId) =>
		_tokens.GetValueOrDefault(sessionId);

	public void SetToken(string sessionId, SpotifyTokenSession token)
	{
		_tokens[sessionId] = token;
		PersistTokens();
	}

	public void RemoveToken(string sessionId)
	{
		if (_tokens.TryRemove(sessionId, out _))
			PersistTokens();
	}

	public void SetPending(string state, PendingSpotifyAuthorization authorization)
	{
		PrunePending();
		_pending[state] = authorization;
		PersistPending();
	}

	public PendingSpotifyAuthorization? TakePending(string state)
	{
		if (!_pending.TryRemove(state, out var pending))
			return null;

		PersistPending();
		if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
			return null;

		return pending;
	}

	public SemaphoreSlim GetRefreshLock(string sessionId) =>
		_refreshLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));

	private void PrunePending()
	{
		foreach (var item in _pending.Where(item => item.Value.ExpiresAt <= DateTimeOffset.UtcNow))
			_pending.TryRemove(item.Key, out _);
	}

	private void LoadTokens()
	{
		if (!File.Exists(_tokenStoragePath))
			return;

		try
		{
			var protectedSessions = JsonSerializer.Deserialize<Dictionary<string, string>>(
				File.ReadAllText(_tokenStoragePath)) ?? [];

			foreach (var (sessionId, protectedToken) in protectedSessions)
			{
				try
				{
					var token = JsonSerializer.Deserialize<SpotifyTokenSession>(_tokenProtector.Unprotect(protectedToken));
					if (token is not null)
						_tokens[sessionId] = token;
				}
				catch (Exception exception) when (exception is CryptographicException or JsonException)
				{
					_logger.LogWarning(exception, "A saved Spotify session could not be restored and was ignored.");
				}
			}
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
		{
			_logger.LogWarning(exception, "Spotify sessions could not be loaded from disk.");
		}
	}

	private void LoadPending()
	{
		if (!File.Exists(_pendingStoragePath))
			return;

		try
		{
			var protectedAuthorizations = JsonSerializer.Deserialize<Dictionary<string, string>>(
				File.ReadAllText(_pendingStoragePath)) ?? [];

			foreach (var (state, protectedAuthorization) in protectedAuthorizations)
			{
				try
				{
					var authorization = JsonSerializer.Deserialize<PendingSpotifyAuthorization>(
						_pendingProtector.Unprotect(protectedAuthorization));
					if (authorization is not null && authorization.ExpiresAt > DateTimeOffset.UtcNow)
						_pending[state] = authorization;
				}
				catch (Exception exception) when (exception is CryptographicException or JsonException)
				{
					_logger.LogWarning(exception, "A pending Spotify authorization could not be restored and was ignored.");
				}
			}
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
		{
			_logger.LogWarning(exception, "Pending Spotify authorizations could not be loaded from disk.");
		}
	}

	private void PersistTokens()
	{
		PersistProtected(
			_tokenStoragePath,
			_tokens,
			_tokenProtector);
	}

	private void PersistPending()
	{
		PersistProtected(
			_pendingStoragePath,
			_pending,
			_pendingProtector);
	}

	private void PersistProtected<T>(
		string storagePath,
		ConcurrentDictionary<string, T> values,
		IDataProtector protector)
	{
		lock (_persistenceLock)
		{
			var directory = Path.GetDirectoryName(storagePath)!;
			Directory.CreateDirectory(directory);
			var protectedValues = values.ToDictionary(
				item => item.Key,
				item => protector.Protect(JsonSerializer.Serialize(item.Value)),
				StringComparer.Ordinal);
			var temporaryPath = $"{storagePath}.{Guid.NewGuid():N}.tmp";

			try
			{
				File.WriteAllText(temporaryPath, JsonSerializer.Serialize(protectedValues));
				if (!OperatingSystem.IsWindows())
				{
					File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
				}

				File.Move(temporaryPath, storagePath, true);
			}
			finally
			{
				if (File.Exists(temporaryPath))
					File.Delete(temporaryPath);
			}
		}
	}
}