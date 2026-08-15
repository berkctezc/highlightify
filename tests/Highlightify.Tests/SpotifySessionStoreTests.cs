namespace Highlightify.Tests;

public sealed class SpotifySessionStoreTests
{
	[Fact]
	public void Pending_authorization_is_encrypted_restored_and_consumed_once()
	{
		var contentRoot = Path.Combine(Path.GetTempPath(), $"highlightify-pending-{Guid.NewGuid():N}");
		Directory.CreateDirectory(contentRoot);

		try
		{
			var provider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(contentRoot, "keys")));
			var environment = new TestHostEnvironment(contentRoot);
			var authorization = new PendingSpotifyAuthorization(
				"session-1",
				"verifier-that-must-not-be-plain-text",
				"http://127.0.0.1/callback",
				"/settings",
				DateTimeOffset.UtcNow.AddMinutes(10));

			var firstStore = new SpotifySessionStore(provider, environment, NullLogger<SpotifySessionStore>.Instance);
			firstStore.SetPending("state-1", authorization);

			var storedJson = File.ReadAllText(Path.Combine(contentRoot, "App_Data", "spotify-pending.json"));
			Assert.DoesNotContain(authorization.CodeVerifier, storedJson, StringComparison.Ordinal);

			var restoredStore = new SpotifySessionStore(provider, environment, NullLogger<SpotifySessionStore>.Instance);
			Assert.Equal(authorization, restoredStore.TakePending("state-1"));

			var consumedStore = new SpotifySessionStore(provider, environment, NullLogger<SpotifySessionStore>.Instance);
			Assert.Null(consumedStore.TakePending("state-1"));
		}
		finally
		{
			Directory.Delete(contentRoot, true);
		}
	}

	private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Development;
		public string ApplicationName { get; set; } = "Highlightify.Tests";
		public string ContentRootPath { get; set; } = contentRoot;
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}