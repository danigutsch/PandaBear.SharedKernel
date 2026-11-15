using System.Diagnostics;

namespace PandaBear.Processes;

/// <summary>
/// Removes inherited environment variables from a child process, so that a value the host holds, such as a token or a
/// variable that redirects Git, does not reach the child.
/// </summary>
public static class ProcessEnvironment
{
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
