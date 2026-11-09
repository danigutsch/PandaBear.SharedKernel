namespace PandaBear.Repository.Tests;

/// <summary>Builds <c>local-check</c> command lines and reads its report.</summary>
internal static class LocalCheckReport
{
    /// <summary>Returns a <c>local-check</c> command line against <c>origin/main</c> with <paramref name="extra"/> after it.</summary>
    public static string[] Check(params string[] extra) => ["local-check", "--base", "origin/main", .. extra];

    /// <summary>Returns the report line of the lane <paramref name="name"/>.</summary>
    public static string Lane(string output, string name) =>
        output.Split('\n').Single(line => line.StartsWith("  " + name + " ", StringComparison.Ordinal));
}
