using System.Text;

namespace SharedKernel.RepoConfig.Tests;

internal static class RoadmapAutomationTestGit
{
    public static RoadmapGitRepository Create(byte[]? status = null)
    {
        return new RoadmapGitRepository((startInfo, _) =>
        {
            var output = startInfo.ArgumentList[0] == "rev-parse"
                ? Encoding.UTF8.GetBytes(RoadmapAutomationTestPlan.BaseSha + "\n")
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
