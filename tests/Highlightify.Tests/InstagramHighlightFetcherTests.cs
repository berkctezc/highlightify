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
	public void ExtractCandidates_ReadsInstagramStoryMusicStickerArtistAndDuration()
	{
		const string html = """
		                    <html><body>
		                    <script type="application/json">
		                    {
		                      "music_asset_info": {
		                        "title": "Bye Bye Bye",
		                        "display_artist": "*NSYNC"
		                      }
		                    }
		                    </script>
		                    </body></html>
		                    """;
		const string apiJson = """
		                       {
		                         "story_music_stickers": [{
		                           "music_asset_info": {
		                             "title": "Bye Bye Bye",
		                             "display_artist": "*NSYNC",
		                             "duration_in_ms": 199253,
		                             "cover_artwork_uri": "https://scontent.example.fbcdn.net/bye-bye-bye.jpg",
		                             "audio_asset_id": "1076076147639666"
		                           }
		                         }]
		                       }
		                       """;

		var candidate = Assert.Single(new InstagramHighlightFetcher().ExtractCandidates([html, apiJson], "story.html"));

		Assert.Equal("Bye Bye Bye", candidate.Title);
		Assert.Equal("*NSYNC", candidate.Artist);
		Assert.Equal(199253, candidate.DurationMs);
		Assert.Equal("https://scontent.example.fbcdn.net/bye-bye-bye.jpg", candidate.ArtworkUrl);
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