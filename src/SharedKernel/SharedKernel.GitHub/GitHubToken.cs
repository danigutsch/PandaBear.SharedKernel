using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.GitHub;

/// <summary>
/// The GitHub token that a command reads from <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c>, in that order, as
/// <see href="https://cli.github.com/manual/gh_help_environment">gh</see> does for <c>github.com</c>. Tokens are never
/// arguments, and no child process receives them: removal covers every forge token variable of
/// <see cref="ProcessEnvironment.ForgeTokenVariableNames"/>, including the GitHub Enterprise Server variables
/// <c>GH_ENTERPRISE_TOKEN</c> and <c>GITHUB_ENTERPRISE_TOKEN</c>, which are never read.
/// </summary>
public static class GitHubToken
{
    /// <summary>
    /// Gets the names of the variables that <see cref="Find"/> reads for <c>github.com</c>, in the order they are read.
    /// An Enterprise Server token is not among them, so it is never sent to <c>api.github.com</c>; the variables removed
    /// from a child are <see cref="ProcessEnvironment.ForgeTokenVariableNames"/>.
    /// </summary>
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

    /// <summary>
    /// Reports whether <paramref name="name"/> names a token variable that must not reach a child, compared without
    /// regard to case: any of <see cref="ProcessEnvironment.ForgeTokenVariableNames"/>.
    /// </summary>
    /// <param name="name">An environment variable name, such as one a configuration file asks to forward to a child process.</param>
    /// <returns>
    /// <see langword="true"/> for <c>GH_TOKEN</c>, <c>GITHUB_TOKEN</c>, <c>GH_ENTERPRISE_TOKEN</c>,
    /// <c>GITHUB_ENTERPRISE_TOKEN</c>, or another forge token variable, in any case.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static bool IsTokenVariable(string name) => ProcessEnvironment.IsForgeTokenVariable(name);

    /// <summary>
    /// Removes every forge token variable of <see cref="ProcessEnvironment.ForgeTokenVariableNames"/>, in any case,
    /// including the GitHub Enterprise Server ones, from the environment that <paramref name="startInfo"/> gives the child.
    /// </summary>
    /// <param name="startInfo">The start information of a child process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public static void RemoveFrom(ProcessStartInfo startInfo) => ProcessEnvironment.RemoveForgeTokens(startInfo);
}
