namespace Highlightify.Web.Services;

public sealed partial class ArtworkSimilarityService(
	IHttpClientFactory httpClientFactory,
	ILogger<ArtworkSimilarityService> logger)
{
	private const int MaximumImageBytes = 4 * 1024 * 1024;
	private readonly string? _magickPath = FindExecutable("magick");

	public async Task<IReadOnlyList<SpotifyTrackResponse>> ApplyAsync(
		TrackCandidate candidate,
		IReadOnlyList<SpotifyTrackResponse> tracks,
		CancellationToken cancellationToken)
	{
		if (_magickPath is null ||
		    tracks.Count == 0 ||
		    !TryGetAllowedUri(candidate.ArtworkUrl, IsInstagramArtworkHost, out var sourceUri))
			return tracks;

		var tempDirectory = Path.Combine(Path.GetTempPath(), $"highlightify-artwork-{Guid.NewGuid():N}");
		Directory.CreateDirectory(tempDirectory);

		try
		{
			var sourcePath = Path.Combine(tempDirectory, "source-image");
			if (!await DownloadImageAsync(sourceUri, sourcePath, IsInstagramArtworkHost, cancellationToken))
				return tracks;

			var ranked = new List<SpotifyTrackResponse>(tracks.Count);
			for (var index = 0; index < tracks.Count; index++)
			{
				var track = tracks[index];
				if (!TryGetAllowedUri(track.ImageUrl, IsSpotifyArtworkHost, out var targetUri))
				{
					ranked.Add(track);
					continue;
				}

				var targetPath = Path.Combine(tempDirectory, $"spotify-image-{index}");
				if (!await DownloadImageAsync(targetUri, targetPath, IsSpotifyArtworkHost, cancellationToken))
				{
					ranked.Add(track);
					continue;
				}

				var distance = await CompareAsync(sourcePath, targetPath, cancellationToken);
				var artworkBonus = distance is null ? 0 : ScoreDistance(distance.Value);
				ranked.Add(track with { MatchScore = track.MatchScore + artworkBonus });
			}

			return ranked;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogDebug("Album artwork comparison timed out; metadata-only ranking will be used");
			return tracks;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogDebug(exception, "Album artwork comparison failed; metadata-only ranking will be used");
			return tracks;
		}
		finally
		{
			TryDeleteDirectory(tempDirectory);
		}
	}

	private async Task<bool> DownloadImageAsync(
		Uri uri,
		string destination,
		Func<string, bool> isAllowedHost,
		CancellationToken cancellationToken)
	{
		using var response = await httpClientFactory.CreateClient("artwork").GetAsync(
			uri,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);
		if (!response.IsSuccessStatusCode ||
		    response.RequestMessage?.RequestUri is not { } finalUri ||
		    finalUri.Scheme != Uri.UriSchemeHttps ||
		    !isAllowedHost(finalUri.Host) ||
		    response.Content.Headers.ContentLength is > MaximumImageBytes)
			return false;

		var mediaType = response.Content.Headers.ContentType?.MediaType;
		if (mediaType is not null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
			return false;

		await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var output = new FileStream(
			destination,
			FileMode.CreateNew,
			FileAccess.Write,
			FileShare.None,
			81920,
			FileOptions.Asynchronous);
		var buffer = new byte[81920];
		var totalBytes = 0;
		while (true)
		{
			var read = await source.ReadAsync(buffer, cancellationToken);
			if (read == 0)
				break;
			totalBytes += read;
			if (totalBytes > MaximumImageBytes)
				return false;
			await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		return totalBytes > 0;
	}

	private async Task<double?> CompareAsync(
		string sourcePath,
		string targetPath,
		CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = _magickPath!,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		foreach (var argument in new[]
		         {
		         	"compare", "-metric", "RMSE",
		         	"(", sourcePath, "-gravity", "south", "-crop", "100%x72%+0+0", "+repage", "-resize", "64x64!", ")",
		         	"(", targetPath, "-gravity", "south", "-crop", "100%x72%+0+0", "+repage", "-resize", "64x64!", ")",
		         	"null:"
		         })
			startInfo.ArgumentList.Add(argument);

		using var process = Process.Start(startInfo);
		if (process is null)
			return null;

		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(5));
		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			if (!process.HasExited)
				process.Kill(entireProcessTree: true);
			throw;
		}

		await stdoutTask;
		var error = await stderrTask;
		if (process.ExitCode is not (0 or 1))
			return null;

		var match = NormalizedDistanceRegex().Match(error);
		return match.Success && double.TryParse(
			match.Groups["distance"].Value,
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var distance)
			? distance
			: null;
	}

	private static int ScoreDistance(double distance) => distance switch
	{
		<= 0.24 => 30,
		<= 0.32 => 24,
		<= 0.40 => 16,
		<= 0.50 => 8,
		<= 0.60 => 3,
		_ => 0
	};

	private static bool TryGetAllowedUri(
		string? value,
		Func<string, bool> isAllowedHost,
		out Uri uri)
	{
		if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
		    parsed.Scheme == Uri.UriSchemeHttps &&
		    isAllowedHost(parsed.Host))
		{
			uri = parsed;
			return true;
		}

		uri = null!;
		return false;
	}

	private static bool IsInstagramArtworkHost(string host) =>
		host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase) ||
		host.EndsWith(".cdninstagram.com", StringComparison.OrdinalIgnoreCase);

	private static bool IsSpotifyArtworkHost(string host) =>
		host.Equals("i.scdn.co", StringComparison.OrdinalIgnoreCase) ||
		host.EndsWith(".scdn.co", StringComparison.OrdinalIgnoreCase);

	private static string? FindExecutable(string name)
	{
		var path = Environment.GetEnvironmentVariable("PATH");
		return string.IsNullOrWhiteSpace(path)
			? null
			: path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(directory => Path.Combine(directory, name))
				.FirstOrDefault(File.Exists);
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, recursive: true);
		}
		catch
		{
			// Best-effort cleanup only.
		}
	}

	[GeneratedRegex(@"\((?<distance>(?:0|1)(?:\.\d+)?)\)", RegexOptions.CultureInvariant)]
	private static partial Regex NormalizedDistanceRegex();
}