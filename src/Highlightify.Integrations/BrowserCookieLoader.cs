using Microsoft.Data.Sqlite;

namespace Highlightify.Integrations;

public static class BrowserCookieLoader
{
	public static string ReadInstagramCookieHeader(string browserSpec)
	{
		var normalized = NormalizeBrowserSpec(browserSpec)
		                 ?? throw new ArgumentException("A browser source is required.", nameof(browserSpec));
		var separator = normalized.IndexOf(':');
		var browser = separator < 0 ? normalized : normalized[..separator];
		if (!browser.Equals("firefox", StringComparison.OrdinalIgnoreCase))
			throw new NotSupportedException(
				"Direct browser authentication in the CLI currently supports Firefox profiles. "
				+ "For another browser, use --instagram-cookie-file with a cookies.txt export.");

		var profilePath = separator < 0 ? FindDefaultFirefoxProfile() : normalized[(separator + 1)..];
		if (string.IsNullOrWhiteSpace(profilePath))
			throw new InvalidOperationException(
				"No Firefox profile was supplied. Pass firefox:/path/to/profile or use --instagram-cookie-file.");

		var databasePath = Path.Combine(profilePath, "cookies.sqlite");
		if (!File.Exists(databasePath))
			throw new FileNotFoundException("Firefox cookies.sqlite was not found in the selected profile.", databasePath);
		try
		{
			using var databaseFile = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		}
		catch (UnauthorizedAccessException exception)
		{
			throw MacosPrivacyException(exception);
		}

		try
		{
			using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = """
			                      SELECT name, value
			                      FROM moz_cookies
			                      WHERE (host = 'instagram.com' OR host LIKE '%.instagram.com')
			                        AND expiry > unixepoch()
			                      ORDER BY name
			                      """;

			using var reader = command.ExecuteReader();
			var cookies = new List<string>();
			while (reader.Read())
			{
				var name = reader.GetString(0);
				var value = reader.GetString(1);
				if (!string.IsNullOrWhiteSpace(name) && !value.Contains('\r') && !value.Contains('\n'))
					cookies.Add($"{name}={value}");
			}

			var header = string.Join("; ", cookies.Distinct(StringComparer.OrdinalIgnoreCase));
			if (string.IsNullOrWhiteSpace(header))
				throw new InvalidOperationException(
					"No active Instagram cookies were found in the selected Firefox profile. "
					+ "Sign in to Instagram in that profile, then try again.");
			return header;
		}
		catch (UnauthorizedAccessException exception)
		{
			throw MacosPrivacyException(exception);
		}
		catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 8 or 14)
		{
			throw new InvalidOperationException(
				"Firefox cookies could not be read. Close Firefox and try again, or provide --instagram-cookie-file with a fresh cookies.txt export.", exception);
		}
	}

	public static string? NormalizeBrowserSpec(string? browserSpec)
	{
		if (string.IsNullOrWhiteSpace(browserSpec))
			return null;

		var trimmed = browserSpec.Trim();
		var separator = trimmed.IndexOf(':');
		if (separator < 0 || separator == trimmed.Length - 1)
			return trimmed;

		var browser = trimmed[..separator];
		var profilePath = trimmed[(separator + 1)..];
		// A leading backslash before ~ is commonly copied from a shell command
		// intended to prevent shell expansion. Once it reaches this application it
		// is part of the argument, so remove it before resolving the home directory.
		if (profilePath.StartsWith("\\~", StringComparison.Ordinal))
			profilePath = profilePath[1..];
		if (profilePath is "~" or "~/" or "~\\")
			profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		else if (profilePath.StartsWith("~/", StringComparison.Ordinal) || profilePath.StartsWith("~\\", StringComparison.Ordinal))
			profilePath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				profilePath[2..]);

		return $"{browser}:{profilePath}";
	}

	public static string? InferFirefoxBrowserSpec(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;
		path = ExpandHomeDirectory(path);

		if (Directory.Exists(path))
			return $"firefox:{path}";

		if (!File.Exists(path) ||
		    !path.EndsWith("cookies.sqlite", StringComparison.OrdinalIgnoreCase)) return null;
		var profileDir = Path.GetDirectoryName(path);
		return !string.IsNullOrWhiteSpace(profileDir)
			? $"firefox:{profileDir}"
			: null;
	}

	private static string ExpandHomeDirectory(string path) => NormalizeBrowserSpec($"firefox:{path}")?[8..] ?? path;

	private static UnauthorizedAccessException MacosPrivacyException(Exception exception) => new(
		"Firefox cookies could not be read because macOS blocked access to the profile. "
		+ "Grant Full Disk Access to the terminal or IDE running Highlightify, restart it, and try again.", exception);

	private static string? FindDefaultFirefoxProfile()
	{
		var profilesRoot = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"Firefox",
			"Profiles");
		return Directory.Exists(profilesRoot)
			? Directory.EnumerateDirectories(profilesRoot).FirstOrDefault()
			: null;
	}
}