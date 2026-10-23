namespace Highlightify.Integrations;

public static class CookieFileReader
{
	public static string ReadAsCookieHeader(string path)
	{
		return ParseAsCookieHeader(File.ReadAllText(path));
	}

	public static string ParseAsCookieHeader(string content)
	{
		var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
		var pairs = new List<string>();

		foreach (var rawLine in lines)
		{
			var line = rawLine.Trim();
			if (line.Length == 0 || (line.StartsWith('#') && !line.StartsWith("#HttpOnly_", StringComparison.OrdinalIgnoreCase)))
				continue;
			if (line.StartsWith("#HttpOnly_", StringComparison.OrdinalIgnoreCase))
				line = line[1..];

			if (line.Contains('\t'))
			{
				var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (parts.Length >= 7)
				{
					var name = parts[5];
					var value = parts[6];
					if (!string.IsNullOrWhiteSpace(name))
						pairs.Add($"{name}={value}");
				}

				continue;
			}

			if (line.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
				line = line[7..].Trim();

			if (line.Contains('='))
				pairs.Add(line.TrimEnd(';'));
		}

		return string.Join("; ", pairs.Distinct(StringComparer.OrdinalIgnoreCase));
	}
}