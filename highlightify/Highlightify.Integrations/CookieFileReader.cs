namespace Highlightify.Integrations;

public static class CookieFileReader
{
    public static string ReadAsCookieHeader(string path)
    {
        var lines = File.ReadAllLines(path);
        var pairs = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

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

            if (line.Contains('='))
                pairs.Add(line.TrimEnd(';'));
        }

        return string.Join("; ", pairs.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}