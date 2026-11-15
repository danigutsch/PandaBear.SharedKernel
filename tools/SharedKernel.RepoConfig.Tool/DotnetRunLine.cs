namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A <c>dotnet run --project</c> command found in a file.
/// </summary>
/// <param name="Line">The one-based number of the first line of the command.</param>
/// <param name="Project">The project path as written, with <c>./</c> removed and <c>\</c> turned into <c>/</c>.</param>
/// <param name="HasFramework">Whether the command selects a framework with <c>-f</c> or <c>--framework</c>.</param>
internal sealed record DotnetRunLine(int Line, string Project, bool HasFramework);
