namespace PandaBear.Testing.Tests;

/// <summary>Puts a directory with a fake <c>git</c> first on the process <c>PATH</c> for the length of a scope.</summary>
internal static class FakeGitPath
{
    /// <summary>Prepends <paramref name="directory"/> to <c>PATH</c> until the returned scope is disposed.</summary>
    public static EnvironmentVariableScope Prepend(string directory) =>
        new("PATH", directory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
}
