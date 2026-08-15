namespace Highlightify.Tests;

public sealed class SessionIdentityTests
{
	[Fact]
	public void Session_id_is_created_once_and_reused_within_the_request()
	{
		var context = new DefaultHttpContext();

		var first = SessionIdentity.GetOrCreate(context);
		var second = SessionIdentity.GetOrCreate(context);

		Assert.Equal(first, second);
		Assert.Single(context.Response.Headers.SetCookie);
		Assert.Contains("max-age=15552000", context.Response.Headers.SetCookie[0], StringComparison.Ordinal);
	}
}