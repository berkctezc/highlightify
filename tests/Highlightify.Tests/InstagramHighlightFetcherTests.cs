using Highlightify.Integrations;

namespace Highlightify.Tests;

public sealed class InstagramHighlightFetcherTests
{
	[Fact]
	public void ExtractCandidates_ReadsStructuredMusicWithoutCrossPairingFallbackFields()
	{
		const string html = """
		                    <html><body>
		                    <script type="application/json">
		                    {
		                      "highlight_music": [
		                        { "track_name": "Midnight City", "artist_name": "M83", "album_name": "Hurry Up, We're Dreaming" },
		                        { "song_name": "Intro", "display_artist_name": "The xx", "collection_name": "xx" }
		                      ]
		                    }
		                    </script>
		                    </body></html>
		                    """;

		var candidates = new InstagramHighlightFetcher().ExtractCandidates(html, "fixture.html");

		Assert.Collection(candidates,
			first =>
			{
				Assert.Equal("Midnight City", first.Title);
				Assert.Equal("M83", first.Artist);
				Assert.Equal("Hurry Up, We're Dreaming", first.Album);
			},
			second =>
			{
				Assert.Equal("Intro", second.Title);
				Assert.Equal("The xx", second.Artist);
				Assert.Equal("xx", second.Album);
			});
	}

	[Fact]
	public void ExtractCandidates_UsesRegexFallbackForNonJsonPayload()
	{
		const string html = """
		                    <html><body>
		                      data = {"artist_name":"Massive Attack","track_name":"Teardrop"}
		                    </body></html>
		                    """;

		var candidates = new InstagramHighlightFetcher().ExtractCandidates(html, "fallback.html");

		var candidate = Assert.Single(candidates);
		Assert.Equal("Teardrop", candidate.Title);
		Assert.Equal("Massive Attack", candidate.Artist);
	}
}
