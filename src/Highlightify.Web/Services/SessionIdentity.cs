using System.Security.Cryptography;

namespace Highlightify.Web.Services;

public static class SessionIdentity
{
	private const string CookieName = "highlightify.sid";

	public static string GetOrCreate(HttpContext context)
	{
		if (context.Request.Cookies.TryGetValue(CookieName, out var existing) &&
		    existing.Length is >= 32 and <= 128)
			return existing;

		var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
		context.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
		{
			HttpOnly = true,
			IsEssential = true,
			SameSite = SameSiteMode.Lax,
			Secure = context.Request.IsHttps,
			MaxAge = TimeSpan.FromDays(30),
			Path = "/"
		});

		return sessionId;
	}
}
