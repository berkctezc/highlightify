namespace Highlightify.Tests;

public sealed class HighlightSourceResolverTests
{
	[Theory]
	[InlineData("17876436264678750", "https://www.instagram.com/stories/highlights/17876436264678750/")]
	[InlineData("https://www.instagram.com/stories/highlights/123/", "https://www.instagram.com/stories/highlights/123/")]
	public void ResolveInstagramHighlightUrl_ResolvesIdsAndPreservesUrls(string source, string expected)
	{
		Assert.Equal(expected, HighlightSourceResolver.ResolveInstagramHighlightUrl(source));
	}
}
