using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Builds a roadmap Git repository that answers commands from canned data instead of running Git.</summary>
internal static class RoadmapAutomationTestGit
{
    /// <summary>Creates a repository that returns <paramref name="head"/> for <c>rev-parse</c> and <paramref name="status"/> for every other command.</summary>
    /// <param name="status">The status output bytes, or <see langword="null"/> for empty output.</param>
    /// <param name="head">The commit SHA reported for <c>rev-parse</c>.</param>
    public static RoadmapGitRepository Create(byte[]? status = null, string head = RoadmapAutomationTestPlan.BaseSha)
    {
        return new RoadmapGitRepository((startInfo, _) =>
        {
            var output = startInfo.ArgumentList[0] == "rev-parse"
                ? Encoding.UTF8.GetBytes(head + "\n")
                : status ?? [];
            return Task.FromResult(new RoadmapGitCommandResult(
                0,
                output,
                string.Empty,
                OutputLimitExceeded: false,
                ErrorLimitExceeded: false));
        });
    }
}
