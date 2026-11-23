using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>Creates the external commands of the versioning engine with a fake <c>git</c>.</summary>
internal static class FakeGitCommands
{
    /// <summary>Returns commands with the real process runner that find <c>git</c> only in <paramref name="gitDirectory"/> and stop it after <paramref name="gitTimeout"/>.</summary>
    public static ExternalCommands Create(string gitDirectory, TimeSpan gitTimeout) =>
        new(new SystemProcessRunner(), new PathExecutableResolver(gitDirectory, isWindows: false), gitTimeout);
}
