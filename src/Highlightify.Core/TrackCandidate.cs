namespace Highlightify.Core;

public sealed record TrackCandidate(
	string Title,
	string? Artist,
	string? Album,
	string Source,
	int? DurationMs = null,
	string? ArtworkUrl = null)
{
	public string NormalizedKey => string.Join('|', Normalize(Title), Normalize(Artist), Normalize(Album));

	public string DisplayTitle =>
		string.Join(" - ", new[] {Title, Artist, Album}.Where(value => !string.IsNullOrWhiteSpace(value)));

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();
}
