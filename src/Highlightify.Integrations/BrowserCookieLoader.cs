namespace Highlightify.Integrations;

public static class BrowserCookieLoader
{
	public static string? InferFirefoxBrowserSpec(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		if (Directory.Exists(path))
			return $"firefox:{path}";

		if (!File.Exists(path) ||
		    !path.EndsWith("cookies.sqlite", StringComparison.OrdinalIgnoreCase)) return null;
		var profileDir = Path.GetDirectoryName(path);
		return !string.IsNullOrWhiteSpace(profileDir)
			? $"firefox:{profileDir}"
			: null;
	}
}