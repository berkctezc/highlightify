using System.Text.Json.Serialization;

namespace Highlightify.Web.Contracts;

public sealed record AppConfigurationResponse(
	bool SpotifyConfigured,
	bool YtDlpAvailable,
	string DefaultBrowserSource,
	string Version);

public sealed record SpotifyConnectionResponse(
	bool Connected,
	bool Configured,
	SpotifyProfileResponse? Profile);

public sealed record SpotifyProfileResponse(
	string Id,
	string DisplayName,
	string? ImageUrl,
	string? ExternalUrl);

public sealed record SpotifyPlaylistResponse(
	string Id,
	string Name,
	string? ImageUrl,
	int TrackCount,
	bool IsPublic,
	string? ExternalUrl);

public sealed record SpotifyTrackResponse(
	string Id,
	string Uri,
	string Name,
	string Artist,
	string Album,
	string? ImageUrl,
	string? ExternalUrl,
	int DurationMs,
	bool Explicit,
	int MatchScore);

[JsonConverter(typeof(JsonStringEnumConverter<ImportStatus>))]
public enum ImportStatus
{
	Queued,
	Reading,
	Matching,
	Ready,
	Exporting,
	Completed,
	Failed
}

public sealed record ImportTrackResponse(
	string Id,
	string Title,
	string? Artist,
	string? Album,
	string Source,
	SpotifyTrackResponse? Match,
	IReadOnlyList<SpotifyTrackResponse> Alternatives);

public sealed record ImportJobResponse(
	Guid Id,
	ImportStatus Status,
	int Progress,
	string StatusMessage,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	IReadOnlyList<string> Sources,
	IReadOnlyList<ImportTrackResponse> Tracks,
	string? Error,
	string? PlaylistId,
	string? PlaylistUrl);

public sealed record ExportPlaylistRequest(
	string? PlaylistId,
	string? PlaylistName,
	bool IsPublic,
	IReadOnlyList<string> TrackUris);

public sealed record ImportCreatedResponse(Guid Id);

public sealed record ApiErrorResponse(string Error);

public sealed record ImportSourceInput(string Label, string? Url, string? Html);

public sealed record StartImportInput(
	IReadOnlyList<ImportSourceInput> Sources,
	string? BrowserSource);
