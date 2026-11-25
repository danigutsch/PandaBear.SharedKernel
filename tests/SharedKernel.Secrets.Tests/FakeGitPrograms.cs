namespace SharedKernel.Secrets.Tests;

/// <summary>Creates the programs of the secrets repository with the real runner and a fake <c>git</c>.</summary>
internal static class FakeGitPrograms
{
    /// <summary>Returns programs that run <paramref name="git"/> through <see cref="SystemToolRunner"/> and have no age or sops.</summary>
    public static SecretsPrograms Create(string git) => new()
    {
        Runner = new SystemToolRunner(),
        ExecutableResolver = new ExecutableMap(new Dictionary<string, string> { ["git"] = git }),
        Age = static () => throw new InvalidOperationException("age is not used."),
        Sops = static () => throw new InvalidOperationException("sops is not used."),
    };
}
