namespace Highlightify.Web.Services;

public sealed partial class ImportJobService(
	IHttpClientFactory httpClientFactory,
	SpotifyWebService spotify,
	SpotifySessionStore spotifySessions,
	IHostEnvironment environment,
	ILogger<ImportJobService> logger)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly ConcurrentDictionary<Guid, ImportJobState> _jobs = new();
	private readonly Lock _persistenceLock = new();
	private readonly string _storagePath = Path.Combine(environment.ContentRootPath, "App_Data", "imports.json");
	private int _historyLoaded;

	public Guid Start(string sessionId, StartImportInput input)
	{
		EnsureHistoryLoaded();
		ValidateInput(input);
		var now = DateTimeOffset.UtcNow;
		var job = new ImportJobState
		{
			Id = Guid.NewGuid(),
			SessionId = sessionId,
			CreatedAt = now,
			UpdatedAt = now,
			Sources = input.Sources.ToList(),
			BrowserSource = NormalizeBrowserSource(input.BrowserSource),
			Status = ImportStatus.Queued,
			Progress = 2,
			StatusMessage = "Transfer has been queued"
		};

		_jobs[job.Id] = job;
		PersistHistory();
		_ = Task.Run(() => ProcessAsync(job, false));
		return job.Id;
	}

	public ImportJobResponse? Get(string sessionId, Guid id)
	{
		EnsureHistoryLoaded();
		return _jobs.TryGetValue(id, out var job) && job.SessionId == sessionId ? ToResponse(job) : null;
	}

	public IReadOnlyList<ImportJobResponse> GetAll(string sessionId)
	{
		EnsureHistoryLoaded();
		return
		[
			.. _jobs.Values
				.Where(job => job.SessionId == sessionId)
				.OrderByDescending(job => job.CreatedAt)
				.Select(ToResponse)
		];
	}

	public void RetryMatching(string sessionId, Guid id)
	{
		EnsureHistoryLoaded();
		if (!_jobs.TryGetValue(id, out var job) || job.SessionId != sessionId)
			throw new KeyNotFoundException("Transfer not found.");

		lock (job.SyncRoot)
		{
			if (job.Status is ImportStatus.Reading or ImportStatus.Matching or ImportStatus.Exporting)
				throw new InvalidOperationException("Transfer is still processing.");
			if (job.Candidates.Count == 0)
				throw new InvalidOperationException("Cannot find matching track.");

			job.Status = ImportStatus.Queued;
			job.Progress = 45;
			job.Error = null;
			job.StatusMessage = "Spotify matches are being refreshed.";
			job.UpdatedAt = DateTimeOffset.UtcNow;
		}

		PersistHistory();
		_ = Task.Run(() => ProcessAsync(job, true));
	}

	public async Task<ImportJobResponse> ExportAsync(
		string sessionId,
		Guid id,
		ExportPlaylistRequest request,
		CancellationToken cancellationToken)
	{
		EnsureHistoryLoaded();
		if (!_jobs.TryGetValue(id, out var job) || job.SessionId != sessionId)
			throw new KeyNotFoundException("Transfer could not be found.");

		var requestedUris = request.TrackUris.Distinct(StringComparer.Ordinal).ToList() ?? [];
		if (requestedUris.Count == 0)
			throw new ArgumentException("Choose at least one track.");

		var allowedUris = job.Tracks
			.SelectMany(track => track.Alternatives)
			.Select(track => track.Uri)
			.ToHashSet(StringComparer.Ordinal);
		if (requestedUris.Any(uri => !allowedUris.Contains(uri)))
			throw new ArgumentException("Geçersiz Spotify parçası seçildi.");

		lock (job.SyncRoot)
		{
			if (job.Status is not (ImportStatus.Ready or ImportStatus.Completed))
				throw new InvalidOperationException("Transfer is not yet ready.");
			job.Status = ImportStatus.Exporting;
			job.Progress = 92;
			job.StatusMessage = "Playlist is being prepared";
			job.Error = null;
			job.UpdatedAt = DateTimeOffset.UtcNow;
		}

		PersistHistory();

		try
		{
			string playlistId;
			string? playlistUrl;
			if (string.IsNullOrWhiteSpace(request.PlaylistId))
			{
				var created = await spotify.CreatePlaylistAsync(
					sessionId,
					string.IsNullOrWhiteSpace(request.PlaylistName) ? "Instagram Highlights" : request.PlaylistName,
					request.IsPublic,
					cancellationToken);
				playlistId = created.Id;
				playlistUrl = created.Url;
			}
			else
			{
				playlistId = request.PlaylistId.Trim();
				if (!SpotifyIdRegex().IsMatch(playlistId))
					throw new ArgumentException("Spotify playlist id is invalid.");
				playlistUrl = $"https://open.spotify.com/playlist/{playlistId}";
			}

			await spotify.AddTracksAsync(sessionId, playlistId, requestedUris, cancellationToken);
			lock (job.SyncRoot)
			{
				job.Status = ImportStatus.Completed;
				job.Progress = 100;
				job.StatusMessage = $"{requestedUris.Count} tracks have been added to Spotify.";
				job.PlaylistId = playlistId;
				job.PlaylistUrl = playlistUrl;
				job.UpdatedAt = DateTimeOffset.UtcNow;
			}

			PersistHistory();

			return ToResponse(job);
		}
		catch (Exception exception)
		{
			lock (job.SyncRoot)
			{
				job.Status = ImportStatus.Ready;
				job.Progress = 90;
				job.StatusMessage = "Playlist transfer could be retried.";
				job.Error = FriendlyMessage(exception);
				job.UpdatedAt = DateTimeOffset.UtcNow;
			}

			PersistHistory();
			throw;
		}
	}

	private async Task ProcessAsync(ImportJobState job, bool matchOnly)
	{
		try
		{
			if (!matchOnly)
				await ReadSourcesAsync(job);

			await MatchCandidatesAsync(job);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Import job {ImportJobId} failed", job.Id);
			lock (job.SyncRoot)
			{
				job.Status = ImportStatus.Failed;
				job.StatusMessage = "Transfer could not be completed.";
				job.Error = FriendlyMessage(exception);
				job.UpdatedAt = DateTimeOffset.UtcNow;
			}

			PersistHistory();
		}
	}

	private async Task ReadSourcesAsync(ImportJobState job)
	{
		Update(job, ImportStatus.Reading, 8, "Reading Instagram sources");
		var candidates = new List<TrackCandidate>();
		var client = httpClientFactory.CreateClient("instagram");
		var fetcher = new InstagramHighlightFetcher(client);

		for (var index = 0; index < job.Sources.Count; index++)
		{
			var source = job.Sources[index];
			IReadOnlyList<TrackCandidate> found;
			if (!string.IsNullOrWhiteSpace(source.Html))
			{
				found = fetcher.ExtractCandidates(source.Html, source.Label);
			}
			else
			{
				var url = HighlightSourceResolver.ResolveInstagramHighlightUrl(source.Url!);
				found = string.IsNullOrWhiteSpace(job.BrowserSource)
					? await fetcher.FetchCandidatesAsync(url, null)
					: await fetcher.FetchCandidatesViaYtDlpAsync(url, job.BrowserSource);
			}

			candidates.AddRange(found);
			var progress = 10 + (int) Math.Round((index + 1d) / job.Sources.Count * 30);
			Update(job, ImportStatus.Reading, progress, $"{index + 1}/{job.Sources.Count} sources have been read");
		}

		var distinct = candidates.DistinctBy(candidate => candidate.NormalizedKey).ToList();
		if (distinct.Count == 0)
			throw new InvalidOperationException("Could not find track info in sources.If it is private try browser authentication.");

		lock (job.SyncRoot)
		{
			job.Candidates = distinct;
			job.Tracks = distinct.Select(ToUnmatchedTrack).ToList();
			job.UpdatedAt = DateTimeOffset.UtcNow;
		}
	}

	private async Task MatchCandidatesAsync(ImportJobState job)
	{
		if (spotifySessions.GetToken(job.SessionId) is null)
		{
			Update(job, ImportStatus.Ready, 90, $"{job.Candidates.Count} tracks have been found — to sync connect Spotify");
			return;
		}

		Update(job, ImportStatus.Matching, 45, "Tracks are being synced to Spotify");
		var tracks = new List<ImportTrackResponse>();
		for (var index = 0; index < job.Candidates.Count; index++)
		{
			var candidate = job.Candidates[index];
			var alternatives = await spotify.FindTrackMatchesAsync(job.SessionId, candidate, CancellationToken.None);
			var bestMatch = alternatives.FirstOrDefault(match => match.MatchScore >= 60);
			tracks.Add(new ImportTrackResponse(
				CreateTrackId(candidate),
				candidate.Title,
				candidate.Artist,
				candidate.Album,
				candidate.Source,
				bestMatch,
				alternatives));

			lock (job.SyncRoot)
			{
				job.Tracks = [.. tracks, .. job.Candidates.Skip(index + 1).Select(ToUnmatchedTrack)];
			}

			var progress = 45 + (int) Math.Round((index + 1d) / job.Candidates.Count * 45);
			Update(job, ImportStatus.Matching, progress, $"{index + 1}/{job.Candidates.Count} tracks have been matched");
		}

		var matchedCount = tracks.Count(track => track.Match is not null);
		Update(job, ImportStatus.Ready, 90, $"{matchedCount}/{tracks.Count} tracks have been matched on Spotify");
	}

	private static ImportTrackResponse ToUnmatchedTrack(TrackCandidate candidate)
	{
		return new ImportTrackResponse(CreateTrackId(candidate), candidate.Title, candidate.Artist, candidate.Album, candidate.Source, null, []);
	}

	private static string CreateTrackId(TrackCandidate candidate)
	{
		return Convert.ToHexString(SHA256.HashData(
			Encoding.UTF8.GetBytes(candidate.NormalizedKey)))[..16].ToLowerInvariant();
	}

	private static ImportJobResponse ToResponse(ImportJobState job)
	{
		lock (job.SyncRoot)
		{
			return new ImportJobResponse(
				job.Id,
				job.Status,
				job.Progress,
				job.StatusMessage,
				job.CreatedAt,
				job.UpdatedAt,
				[.. job.Sources.Select(source => source.Label)],
				[.. job.Tracks],
				job.Error,
				job.PlaylistId,
				job.PlaylistUrl);
		}
	}

	private void Update(ImportJobState job, ImportStatus status, int progress, string message)
	{
		lock (job.SyncRoot)
		{
			job.Status = status;
			job.Progress = Math.Clamp(progress, 0, 100);
			job.StatusMessage = message;
			job.Error = null;
			job.UpdatedAt = DateTimeOffset.UtcNow;
		}

		PersistHistory();
	}

	private void EnsureHistoryLoaded()
	{
		if (Interlocked.Exchange(ref _historyLoaded, 1) == 1)
			return;

		try
		{
			if (!File.Exists(_storagePath))
				return;
			var storedJobs = JsonSerializer.Deserialize<List<StoredImportJob>>(File.ReadAllText(_storagePath), JsonOptions) ?? [];
			foreach (var state in from stored in storedJobs
			         let wasInterrupted = stored.Status is ImportStatus.Queued or ImportStatus.Reading or ImportStatus.Matching or ImportStatus.Exporting
			         select new ImportJobState
			         {
				         Id = stored.Id,
				         SessionId = stored.SessionId,
				         CreatedAt = stored.CreatedAt,
				         UpdatedAt = stored.UpdatedAt,
				         Sources = [.. stored.SourceLabels.Select(label => new ImportSourceInput(label, null, null))],
				         Status = wasInterrupted ? ImportStatus.Failed : stored.Status,
				         Progress = stored.Progress,
				         StatusMessage = wasInterrupted ? "Transfer has been stopped because of app shutdown." : stored.StatusMessage,
				         Candidates = stored.Candidates,
				         Tracks = stored.Tracks,
				         Error = wasInterrupted ? "Transfer has been interrupted. Try adding sources again." : stored.Error,
				         PlaylistId = stored.PlaylistId,
				         PlaylistUrl = stored.PlaylistUrl
			         })
			{
				_jobs[state.Id] = state;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Import history could not be loaded from {StoragePath}", _storagePath);
		}
	}

	private void PersistHistory()
	{
		try
		{
			lock (_persistenceLock)
			{
				var snapshot = _jobs.Values
					.OrderByDescending(job => job.CreatedAt)
					.Take(100)
					.Select(job =>
					{
						lock (job.SyncRoot)
						{
							return new StoredImportJob(
								job.Id,
								job.SessionId,
								job.CreatedAt,
								job.UpdatedAt,
								[.. job.Sources.Select(source => source.Label)],
								job.Status,
								job.Progress,
								job.StatusMessage,
								[.. job.Candidates],
								[.. job.Tracks],
								job.Error,
								job.PlaylistId,
								job.PlaylistUrl);
						}
					})
					.ToList();

				Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
				var temporaryPath = $"{_storagePath}.{Guid.NewGuid():N}.tmp";
				File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
				File.Move(temporaryPath, _storagePath, true);
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Import history could not be saved to {StoragePath}", _storagePath);
		}
	}

	private static void ValidateInput(StartImportInput input)
	{
		if (input.Sources is null || input.Sources.Count == 0)
			throw new ArgumentException("At least one Instagram highlight url or an HTML file is required for sync.");
		if (input.Sources.Count > 12)
			throw new ArgumentException("Only up to 12 sources are supported at once.");

		foreach (var source in input.Sources)
		{
			if (string.IsNullOrWhiteSpace(source.Label) || source.Label.Length > 300)
				throw new ArgumentException("Invalid source name.");
			if (!string.IsNullOrWhiteSpace(source.Html))
			{
				if (source.Html.Length > 6_000_000)
					throw new ArgumentException($"{source.Label} file exceed 6MB.");
				continue;
			}

			if (string.IsNullOrWhiteSpace(source.Url))
				throw new ArgumentException("Source URL cannot be empty.");
			ValidateInstagramSource(source.Url);
		}

		NormalizeBrowserSource(input.BrowserSource);
	}

	private static void ValidateInstagramSource(string source)
	{
		if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
		{
			if (!HighlightIdRegex().IsMatch(source.Trim('/')))
				throw new ArgumentException($"Invalid Instagram Highlight Id or URL: {source}");
			return;
		}

		if (uri.Scheme is not ("http" or "https") ||
		    !(uri.Host.Equals("instagram.com", StringComparison.OrdinalIgnoreCase) ||
		      uri.Host.EndsWith(".instagram.com", StringComparison.OrdinalIgnoreCase)))
			throw new ArgumentException("Only instagram.com highlights are supported.");
	}

	private static string? NormalizeBrowserSource(string? browserSource)
	{
		if (string.IsNullOrWhiteSpace(browserSource) || browserSource.Equals("none", StringComparison.OrdinalIgnoreCase))
			return null;
		var trimmed = browserSource.Trim();
		if (trimmed.Length > 500 || trimmed.Contains('\n') || !BrowserSourceRegex().IsMatch(trimmed))
			throw new ArgumentException("Browser source can only be brave, firefox, chrome, chromium, edge or safari.");
		return trimmed;
	}

	private static string FriendlyMessage(Exception exception)
	{
		return exception switch
		{
			UnauthorizedAccessException => exception.Message,
			ArgumentException => exception.Message,
			HttpRequestException => "Cannot access Instagram or Spotify APIs. Check network connection and try again.",
			_ => exception.Message.Length <= 500 ? exception.Message : "Unexpected error."
		};
	}

	[GeneratedRegex("^[0-9]{5,40}$", RegexOptions.CultureInvariant)]
	private static partial Regex HighlightIdRegex();

	[GeneratedRegex("^(brave|firefox|chrome|chromium|edge|safari)(:.+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex BrowserSourceRegex();

	[GeneratedRegex("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
	private static partial Regex SpotifyIdRegex();

	private sealed class ImportJobState
	{
		public object SyncRoot { get; } = new();
		public required Guid Id { get; init; }
		public required string SessionId { get; init; }
		public required DateTimeOffset CreatedAt { get; init; }
		public required DateTimeOffset UpdatedAt { get; set; }
		public required List<ImportSourceInput> Sources { get; init; }
		public string? BrowserSource { get; init; }
		public required ImportStatus Status { get; set; }
		public required int Progress { get; set; }
		public required string StatusMessage { get; set; }
		public List<TrackCandidate> Candidates { get; set; } = [];
		public List<ImportTrackResponse> Tracks { get; set; } = [];
		public string? Error { get; set; }
		public string? PlaylistId { get; set; }
		public string? PlaylistUrl { get; set; }
	}

	private sealed record StoredImportJob(
		Guid Id,
		string SessionId,
		DateTimeOffset CreatedAt,
		DateTimeOffset UpdatedAt,
		IReadOnlyList<string> SourceLabels,
		ImportStatus Status,
		int Progress,
		string StatusMessage,
		List<TrackCandidate> Candidates,
		List<ImportTrackResponse> Tracks,
		string? Error,
		string? PlaylistId,
		string? PlaylistUrl);
}