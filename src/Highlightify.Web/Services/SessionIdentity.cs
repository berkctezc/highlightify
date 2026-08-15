namespace Highlightify.Web.Services;

public static class SessionIdentity
{
	private const string CookieName = "highlightify.sid";
	private static readonly object ContextItemKey = new();

	public static string GetOrCreate(HttpContext context)
	{
		if (context.Items.TryGetValue(ContextItemKey, out var cached) && cached is string cachedSessionId)
			return cachedSessionId;

		if (context.Request.Cookies.TryGetValue(CookieName, out var existing) &&
		    existing.Length == 48 &&
		    existing.All(Uri.IsHexDigit))
		{
			context.Items[ContextItemKey] = existing;
			return existing;
		}

		var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
		context.Items[ContextItemKey] = sessionId;
		context.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
		{
			HttpOnly = true,
			IsEssential = true,
			SameSite = SameSiteMode.Lax,
			Secure = context.Request.IsHttps,
			MaxAge = TimeSpan.FromDays(180),
			Expires = DateTimeOffset.UtcNow.AddDays(180),
			Path = "/"
		});

		return sessionId;
	}
}