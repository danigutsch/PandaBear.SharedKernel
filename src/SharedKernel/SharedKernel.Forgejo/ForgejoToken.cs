using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.Forgejo;

/// <summary>
/// The Forgejo token that a command reads from <c>FORGEJO_TOKEN</c>, the variable that
/// <see href="https://forgejo.org/docs/latest/user/actions/reference/">Forgejo Actions</see> sets to the automatic token of
/// a run. Tokens are never arguments, and no child process receives them.
/// </summary>
public static class ForgejoToken
{
    /// <summary>Gets the names of the variables that hold a Forgejo token, in the order they are read.</summary>
    public static IReadOnlyList<string> VariableNames { get; } = ["FORGEJO_TOKEN"];

    /// <summary>Returns the token of <c>FORGEJO_TOKEN</c> when it is not empty or white-space, or <see langword="null"/>.</summary>
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
    /// <returns><see langword="true"/> for <c>FORGEJO_TOKEN</c> in any case.</returns>
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
