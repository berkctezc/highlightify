namespace Highlightify.Console;

public static class Program
{
	public static Task<int> Main(string[] args) => HighlightifyApp.RunAsync(args);
}