namespace PandaBear.Secrets.Tool;

/// <summary>Finds the programs the commands run, in the absolute directories of <c>PATH</c> only.</summary>
internal static class ToolLocator
{
    /// <summary>Returns the absolute path of <paramref name="command"/>.</summary>
    /// <exception cref="InvalidOperationException">The program is not on <c>PATH</c>.</exception>
    public static string Require(SecretsEnvironment environment, string command) =>
        environment.ResolveExecutable(command) ?? throw new InvalidOperationException($"{command} was not found on PATH.");

    /// <summary>Returns the age adapter; <c>age</c> and <c>age-keygen</c> ship together, so both must be found.</summary>
    /// <exception cref="InvalidOperationException">A program is not on <c>PATH</c>.</exception>
    public static ProcessAgeTool Age(SecretsEnvironment environment) =>
        new(environment.Runner, Require(environment, "age"), Require(environment, "age-keygen"));

    /// <summary>Returns the sops adapter.</summary>
    /// <exception cref="InvalidOperationException">The program is not on <c>PATH</c>.</exception>
    public static ProcessSopsTool Sops(SecretsEnvironment environment) => new(environment.Runner, Require(environment, "sops"));
}
