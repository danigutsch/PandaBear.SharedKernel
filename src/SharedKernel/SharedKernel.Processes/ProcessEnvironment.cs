using System.Diagnostics;

namespace PandaBear.Processes;

/// <summary>
/// Removes inherited environment variables from a child process, so that a value the host holds, such as a token or a
/// variable that redirects Git, does not reach the child.
/// </summary>
public static class ProcessEnvironment
{
    /// <summary>
    /// Gets the names of the variables that hold a forge token: <c>GH_TOKEN</c> and <c>GITHUB_TOKEN</c>, which
    /// <see href="https://cli.github.com/manual/gh_help_environment">gh</see> reads for <c>github.com</c>;
    /// <c>FORGEJO_TOKEN</c>, which <see href="https://forgejo.org/docs/latest/user/actions/reference/">Forgejo Actions</see>
    /// sets; and <c>GH_ENTERPRISE_TOKEN</c> and <c>GITHUB_ENTERPRISE_TOKEN</c>, which gh reads for a GitHub Enterprise
    /// Server host.
    /// </summary>
    public static IReadOnlyList<string> ForgeTokenVariableNames { get; } =
        ["GH_TOKEN", "GITHUB_TOKEN", "FORGEJO_TOKEN", "GH_ENTERPRISE_TOKEN", "GITHUB_ENTERPRISE_TOKEN"];

    /// <summary>
    /// Reports whether <paramref name="name"/> is one of <see cref="ForgeTokenVariableNames"/>, compared without regard to
    /// case.
    /// </summary>
    /// <param name="name">An environment variable name, such as one a configuration file asks to set for a child process.</param>
    /// <returns><see langword="true"/> for a forge token variable in any case.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static bool IsForgeTokenVariable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ForgeTokenVariableNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes every variable of <see cref="ForgeTokenVariableNames"/>, in any case, from the environment that
    /// <paramref name="startInfo"/> gives the child, so a child that needs no forge access never receives a token of the
    /// host, whichever forge it came from.
    /// </summary>
    /// <param name="startInfo">The start information of a child process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public static void RemoveForgeTokens(ProcessStartInfo startInfo) => Remove(startInfo, ForgeTokenVariableNames);

    /// <summary>
    /// Removes every variable named in <paramref name="names"/>, compared without regard to case, from the environment
    /// that <paramref name="startInfo"/> gives the child.
    /// </summary>
    /// <param name="startInfo">The start information of a child process.</param>
    /// <param name="names">The variable names to remove.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Remove(ProcessStartInfo startInfo, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(names);
        var removed = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveWhere(startInfo, removed.Contains);
    }

    /// <summary>
    /// Removes every <c>GIT_*</c> variable, in any case, from the environment that <paramref name="startInfo"/> gives the
    /// child. Variables such as <c>GIT_DIR</c>, <c>GIT_INDEX_FILE</c>, or <c>GIT_CONFIG_*</c> would otherwise point Git at
    /// another repository, index, or configuration.
    /// </summary>
    /// <param name="startInfo">The start information of a Git process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public static void RemoveGitVariables(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        RemoveWhere(startInfo, static name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveWhere(ProcessStartInfo startInfo, Func<string, bool> predicate)
    {
        foreach (var name in startInfo.Environment.Keys.Where(predicate).ToArray())
        {
            startInfo.Environment.Remove(name);
        }
    }
}
