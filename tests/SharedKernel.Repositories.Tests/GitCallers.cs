using PandaBear.Processes;

namespace PandaBear.Repositories.Tests;

/// <summary>Runs each git caller of the library against a fake git and reports how it ended.</summary>
internal static class GitCallers
{
    /// <summary>Returns a git runner that finds <c>git</c> only in <paramref name="directory"/> and stops it after <paramref name="timeout"/>.</summary>
    public static GitProcess FakeGit(string directory, TimeSpan timeout) =>
        new(new PathExecutableResolver(directory, isWindows: false), timeout);

    /// <summary>
    /// Runs the named caller (<c>baseline</c>, <c>tracked-files</c>, <c>roadmap</c>, or <c>text-encoding</c>) and returns
    /// <c>completed</c>, the issues it reported, or the type and message of its failure.
    /// </summary>
    public static async Task<string> Call(string caller, GitProcess git, string root)
    {
        var ct = TestContext.Current.CancellationToken;
        try
        {
            switch (caller)
            {
                case "baseline":
                    _ = await BaselineGit.Plan(git, root, [], ct);
                    return "completed";
                case "tracked-files":
                    _ = await GitTrackedFiles.Read(git, root, ["*"], ct);
                    return "completed";
                case "roadmap":
                    _ = await new RoadmapGitRepository(git).ReadHead(root, ct);
                    return "completed";
                case "text-encoding":
                    var issues = await TextEncodingVerifier.Verify(git, root, ct);
                    return string.Join("; ", issues.Select(static issue => "issue: " + issue.Message));
                default:
                    throw new ArgumentOutOfRangeException(nameof(caller), caller, "Unknown git caller.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            return exception.GetType().Name + ": " + exception.Message;
        }
    }
}
