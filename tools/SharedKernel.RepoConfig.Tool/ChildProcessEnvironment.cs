using System.Diagnostics;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Keeps the GitHub tokens that the tool reads out of the child processes it starts.
/// </summary>
internal static class ChildProcessEnvironment
{
    /// <summary>
    /// Removes <c>GH_TOKEN</c> and <c>GITHUB_TOKEN</c> from the environment that <paramref name="startInfo"/> gives the child process.
    /// </summary>
    /// <param name="startInfo">The start information of a child process.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public static void RemoveGitHubTokens(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        startInfo.Environment.Remove("GH_TOKEN");
        startInfo.Environment.Remove("GITHUB_TOKEN");
    }
}
