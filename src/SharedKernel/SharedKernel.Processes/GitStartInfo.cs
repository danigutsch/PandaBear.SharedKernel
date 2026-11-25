using System.Diagnostics;

namespace SharedKernel.Processes;

/// <summary>
/// Describes a <c>git</c> run that cannot prompt and receives none of the host's credentials or Git redirections: the
/// absolute executable from an absolute <c>PATH</c> directory, the arguments one by one without a shell, both output
/// streams redirected, <c>GIT_TERMINAL_PROMPT=0</c>, and an environment without askpass programs, forge tokens, or
/// inherited <c>GIT_*</c> variables.
/// </summary>
public static class GitStartInfo
{
    private static readonly string[] AskPassVariableNames = ["SSH_ASKPASS", "SSH_ASKPASS_REQUIRE"];

    /// <summary>Gets the time limit for one <c>git</c> call: 30 seconds.</summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Describes a <c>git</c> run, finding <c>git</c> with <see cref="PathExecutableResolver.Default"/>.</summary>
    /// <param name="workingDirectory">The directory that <c>git</c> starts in; it is made absolute.</param>
    /// <param name="arguments">The arguments, passed one by one.</param>
    /// <returns>The start information, which the caller may extend with its own <c>GIT_*</c> variables.</returns>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No absolute <c>PATH</c> directory contains <c>git</c>.</exception>
    public static ProcessStartInfo Create(string workingDirectory, IEnumerable<string> arguments) =>
        Create(workingDirectory, arguments, PathExecutableResolver.Default);

    /// <summary>Describes a <c>git</c> run, finding <c>git</c> with <paramref name="resolver"/>.</summary>
    /// <param name="workingDirectory">The directory that <c>git</c> starts in; it is made absolute.</param>
    /// <param name="arguments">The arguments, passed one by one.</param>
    /// <param name="resolver">Finds the <c>git</c> executable.</param>
    /// <returns>The start information, which the caller may extend with its own <c>GIT_*</c> variables.</returns>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or <paramref name="resolver"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="resolver"/> finds no <c>git</c>, or returns a path that is not fully qualified.</exception>
    public static ProcessStartInfo Create(string workingDirectory, IEnumerable<string> arguments, IExecutableResolver resolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(resolver);

        var git = resolver.Find("git");
        if (git is null || !Path.IsPathFullyQualified(git))
        {
            throw new InvalidOperationException("Could not resolve git to an absolute executable path.");
        }

        ProcessStartInfo startInfo = new(git)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(workingDirectory),
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ApplyEnvironment(startInfo);
        return startInfo;
    }

    /// <summary>
    /// Applies the environment rules of <see cref="Create(string, IEnumerable{string}, IExecutableResolver)"/> to start
    /// information that a caller built itself, such as one whose executable a host's resolver chose: it removes every
    /// inherited <c>GIT_*</c> variable, forge token, <c>SSH_ASKPASS</c>, and <c>SSH_ASKPASS_REQUIRE</c>, and sets
    /// <c>GIT_TERMINAL_PROMPT=0</c>. Variables the caller needs, such as its own <c>GIT_DIR</c>, are set afterwards.
    /// </summary>
    /// <param name="startInfo">The start information of a <c>git</c> process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public static void ApplyEnvironment(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ProcessEnvironment.RemoveGitVariables(startInfo);
        ProcessEnvironment.RemoveForgeTokens(startInfo);
        ProcessEnvironment.Remove(startInfo, AskPassVariableNames);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
    }
}
