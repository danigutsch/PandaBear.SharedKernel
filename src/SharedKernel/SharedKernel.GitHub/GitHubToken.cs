using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.GitHub;

/// <summary>
/// The GitHub token that a command reads from <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c>, in that order, as
/// <see href="https://cli.github.com/manual/gh_help_environment">gh</see> does. Tokens are never arguments, and no
/// child process receives them.
/// </summary>
public static class GitHubToken
{
    /// <summary>Gets the names of the variables that hold a GitHub token, in the order they are read.</summary>
    public static IReadOnlyList<string> VariableNames { get; } = ["GH_TOKEN", "GITHUB_TOKEN"];

    /// <summary>
    /// Returns the first token of <c>GH_TOKEN</c> and <c>GITHUB_TOKEN</c> that is not empty or white-space, or
    /// <see langword="null"/> when neither holds one.
    /// </summary>
    /// <param name="getEnvironmentVariable">Reads a variable, such as <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getEnvironmentVariable"/> is <see langword="null"/>.</exception>
    public static string? Find(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        foreach (var name in VariableNames)
        {
            if (getEnvironmentVariable(name) is { } token && !string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>Reports whether <paramref name="name"/> names a token variable, compared without regard to case.</summary>
    /// <param name="name">An environment variable name, such as one a configuration file asks to forward to a child process.</param>
    /// <returns><see langword="true"/> for <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c> in any case.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static bool IsTokenVariable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return VariableNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Removes the token variables, in any case, from the environment that <paramref name="startInfo"/> gives the child.</summary>
    /// <param name="startInfo">The start information of a child process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public static void RemoveFrom(ProcessStartInfo startInfo) => ProcessEnvironment.Remove(startInfo, VariableNames);
}
