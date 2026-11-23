namespace PandaBear.Processes.Tests;

/// <summary>
/// Sets, for the length of a scope, the process variables that must never reach git: forge tokens, askpass programs, a
/// terminal prompt, and variables that point git at another repository or configuration.
/// </summary>
internal sealed class HostileGitEnvironment : IDisposable
{
    private static readonly Dictionary<string, string> Variables = new(StringComparer.Ordinal)
    {
        ["GH_TOKEN"] = "credential-marker",
        ["GITHUB_TOKEN"] = "credential-marker",
        ["FORGEJO_TOKEN"] = "credential-marker",
        ["GH_ENTERPRISE_TOKEN"] = "credential-marker",
        ["GITHUB_ENTERPRISE_TOKEN"] = "credential-marker",
        ["GIT_ASKPASS"] = "/nonexistent/askpass",
        ["SSH_ASKPASS"] = "/nonexistent/askpass",
        ["SSH_ASKPASS_REQUIRE"] = "force",
        ["GIT_TERMINAL_PROMPT"] = "1",
        ["GIT_DIR"] = "/nonexistent/repository.git",
        ["GIT_CONFIG_PARAMETERS"] = "'core.askPass'='/nonexistent/askpass'",
    };

    private readonly EnvironmentVariableScope[] _scopes;

    /// <summary>Sets every variable in the process environment.</summary>
    public HostileGitEnvironment() =>
        _scopes = [.. Variables.Select(static variable => new EnvironmentVariableScope(variable.Key, variable.Value))];

    /// <summary>
    /// Asserts that an environment, as <c>NAME=value</c> lines, holds none of the variables except
    /// <c>GIT_TERMINAL_PROMPT=0</c>.
    /// </summary>
    public static void ShouldNotReach(IReadOnlyCollection<string> environment)
    {
        environment.ShouldContain("GIT_TERMINAL_PROMPT=0");
        foreach (var name in Variables.Keys.Where(static name => name != "GIT_TERMINAL_PROMPT"))
        {
            environment.ShouldNotContain(line => line.StartsWith(name + "=", StringComparison.Ordinal), $"{name} must not reach git.");
        }
    }

    /// <summary>Restores the previous values in reverse order.</summary>
    public void Dispose()
    {
        foreach (var scope in _scopes.Reverse())
        {
            scope.Dispose();
        }
    }
}
