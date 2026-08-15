namespace Highlightify.Console;

public static class Program
{
	public static Task<int> Main(string[] args)
	{
		return HighlightifyApp.RunAsync(args);
	}
}