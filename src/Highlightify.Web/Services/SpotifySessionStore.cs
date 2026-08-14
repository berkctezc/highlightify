using System.Collections.Concurrent;

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

	public SpotifyTokenSession? GetToken(string sessionId) =>
		_tokens.GetValueOrDefault(sessionId);

	public void SetToken(string sessionId, SpotifyTokenSession token) =>
		_tokens[sessionId] = token;

	public void RemoveToken(string sessionId) =>
		_tokens.TryRemove(sessionId, out _);

	public void SetPending(string state, PendingSpotifyAuthorization authorization)
	{
		PrunePending();
		_pending[state] = authorization;
	}

	public PendingSpotifyAuthorization? TakePending(string state)
	{
		if (!_pending.TryRemove(state, out var pending) || pending.ExpiresAt <= DateTimeOffset.UtcNow)
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
}
